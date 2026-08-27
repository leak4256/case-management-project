using System.Linq.Expressions;
using CaseManagement.Application.DTOs;
using CaseManagement.Application.Interfaces;
using CaseManagement.Application.Queries;
using CaseManagement.Domain.Entities;
using CaseManagement.Domain.Enums;
using CaseManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaseManagement.Infrastructure.Repositories;

public class CaseRepository(CaseManagementDbContext dbContext, TimeProvider timeProvider)
    : ICaseRepository
{
    private static readonly TimeSpan RecentlyUpdatedWindow = TimeSpan.FromDays(7);

    private static readonly Expression<Func<Case, CaseDto>> ProjectToDto = c => new CaseDto
    {
        Id = c.Id,
        Title = c.Title,
        OrganizationName = c.OrganizationName,
        Status = c.Status,
        Priority = c.Priority,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        RowVersion = c.RowVersion
    };

    // One definition, two consumers: EF translates the expression into the list SELECT, while the
    // compiled delegate maps the entity that GetByIdAsync has already materialised.
    private static readonly Func<Case, CaseDto> MapToDto = ProjectToDto.Compile();

    private static readonly CaseSummaryDto EmptySummary = new()
    {
        TotalCount = 0,
        NewCount = 0,
        InProgressCount = 0,
        WaitingCount = 0,
        CompletedCount = 0,
        LowPriorityCount = 0,
        MediumPriorityCount = 0,
        HighPriorityCount = 0,
        AverageOpenAgeInDays = null,
        UpdatedInLastSevenDays = 0
    };

    public async Task<PagedResult<CaseDto>> GetPagedAsync(
        CaseQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var matches = ApplyFilters(dbContext.Cases.AsNoTracking(), parameters);

        var totalCount = await matches.CountAsync(cancellationToken);

        // long, because Page is only validated against int.MaxValue: as an int this product
        // overflows for a large page number and reaches SQL Server as a negative OFFSET.
        var offset = (long)(parameters.Page - 1) * parameters.PageSize;

        IReadOnlyList<CaseDto> items = offset < totalCount
            ? await FetchPageAsync(matches, parameters, (int)offset, cancellationToken)
            : [];

        return new PagedResult<CaseDto>
        {
            Items = items,
            Page = parameters.Page,
            PageSize = parameters.PageSize,
            TotalCount = totalCount
        };
    }

    private static Task<List<CaseDto>> FetchPageAsync(
        IQueryable<Case> matches,
        CaseQueryParameters parameters,
        int offset,
        CancellationToken cancellationToken)
    {
        return ApplySort(matches, parameters)
            .Skip(offset)
            .Take(parameters.PageSize)
            .Select(ProjectToDto)
            .ToListAsync(cancellationToken);
    }

    public async Task<CaseWithVersion?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Selecting the whole entity rather than projecting: this is a seek on the clustered index,
        // so every column already sits on the page the seek lands on.
        var entity = await dbContext.Cases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return entity is null ? null : ToCaseWithVersion(entity);
    }

    public async Task<UpdateCaseStatusOutcome> UpdateStatusAsync(
        int id,
        CaseStatus status,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        // Tracked on purpose: the concurrency check rides on the entity's original RowVersion.
        var entity = await dbContext.Cases
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (entity is null)
        {
            return new UpdateCaseStatusOutcome { Result = UpdateCaseStatusResult.NotFound };
        }

        // Nothing to write means no UPDATE, so SQL Server never gets to compare the versions and
        // the check has to happen here instead — otherwise a stale caller is told it succeeded.
        if (!entity.ChangeStatus(status, timeProvider.GetUtcNow().UtcDateTime))
        {
            return new UpdateCaseStatusOutcome
            {
                Result = entity.RowVersion.SequenceEqual(expectedRowVersion)
                    ? UpdateCaseStatusResult.Updated
                    : UpdateCaseStatusResult.VersionMismatch,
                Current = ToCaseWithVersion(entity)
            };
        }

        // The row EF just read may already be newer than the one the client saw. Overwriting the
        // original value is what puts the *client's* version into the UPDATE's WHERE clause.
        dbContext.Entry(entity).Property(c => c.RowVersion).OriginalValue = expectedRowVersion;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await BuildConflictOutcomeAsync(id, cancellationToken);
        }

        return new UpdateCaseStatusOutcome
        {
            Result = UpdateCaseStatusResult.Updated,
            Current = ToCaseWithVersion(entity)
        };
    }

    private async Task<UpdateCaseStatusOutcome> BuildConflictOutcomeAsync(
        int id,
        CancellationToken cancellationToken)
    {
        // Zero rows matched either because the version moved on or because the row is gone; only
        // a fresh read tells the two apart, and it also supplies the state the client must merge.
        var current = await dbContext.Cases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        return current is null
            ? new UpdateCaseStatusOutcome { Result = UpdateCaseStatusResult.NotFound }
            : new UpdateCaseStatusOutcome
            {
                Result = UpdateCaseStatusResult.VersionMismatch,
                Current = ToCaseWithVersion(current)
            };
    }

    private static CaseWithVersion ToCaseWithVersion(Case entity)
    {
        return new CaseWithVersion
        {
            Case = MapToDto(entity),
            RowVersion = entity.RowVersion
        };
    }

    public async Task<CaseSummaryDto> GetSummaryAsync(
        CaseQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var matches = ApplyFilters(dbContext.Cases.AsNoTracking(), parameters);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var updatedSince = now - RecentlyUpdatedWindow;

        // Grouping on a constant folds the filtered set into one group, so all ten figures come
        // back from a single SELECT instead of one round trip per count.
        var summary = await matches
            .GroupBy(_ => 1)
            .Select(g => new CaseSummaryDto
            {
                TotalCount = g.Count(),
                NewCount = g.Count(c => c.Status == CaseStatus.New),
                InProgressCount = g.Count(c => c.Status == CaseStatus.InProgress),
                WaitingCount = g.Count(c => c.Status == CaseStatus.Waiting),
                CompletedCount = g.Count(c => c.Status == CaseStatus.Completed),
                LowPriorityCount = g.Count(c => c.Priority == CasePriority.Low),
                MediumPriorityCount = g.Count(c => c.Priority == CasePriority.Medium),
                HighPriorityCount = g.Count(c => c.Priority == CasePriority.High),

                // Completed rows select null and SQL AVG skips nulls, so the mean covers open
                // cases only. Hours rather than DATEDIFF(day), which counts midnights crossed.
                AverageOpenAgeInDays = g.Average(c => c.Status == CaseStatus.Completed
                    ? (double?)null
                    : EF.Functions.DateDiffHour(c.CreatedAt, now) / 24.0),

                UpdatedInLastSevenDays = g.Count(c => c.UpdatedAt >= updatedSince)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // No matching rows means no group at all, not a row of zeros.
        if (summary is null)
        {
            return EmptySummary;
        }

        return summary with
        {
            AverageOpenAgeInDays = summary.AverageOpenAgeInDays is { } averageAge
                ? Math.Round(averageAge, 1)
                : null
        };
    }

    private static IQueryable<Case> ApplyFilters(IQueryable<Case> query, CaseQueryParameters parameters)
    {
        var search = parameters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => c.Title.Contains(search) || c.OrganizationName.Contains(search));
        }

        var organization = parameters.Organization?.Trim();
        if (!string.IsNullOrEmpty(organization))
        {
            query = query.Where(c => c.OrganizationName.StartsWith(organization));
        }

        // EF.Constant inlines the values as a literal IN list, which makes the caller's input part
        // of the SQL text — hence Distinct and Order, so a repeated or reordered value cannot
        // stretch the list or compile as a second plan-cache entry.
        if (parameters.Status is { Length: > 0 })
        {
            var statuses = parameters.Status.Distinct().Order().ToArray();
            query = query.Where(c => EF.Constant(statuses).Contains(c.Status));
        }

        if (parameters.Priority is { Length: > 0 })
        {
            var priorities = parameters.Priority.Distinct().Order().ToArray();
            query = query.Where(c => EF.Constant(priorities).Contains(c.Priority));
        }

        if (parameters.CreatedFrom is { } createdFrom)
        {
            query = query.Where(c => c.CreatedAt >= createdFrom);
        }

        if (parameters.CreatedTo is { } createdTo)
        {
            query = query.Where(c => c.CreatedAt <= createdTo);
        }

        return query;
    }

    private static IOrderedQueryable<Case> ApplySort(IQueryable<Case> query, CaseQueryParameters parameters)
    {
        var descending = parameters.SortDirection == SortDirection.Descending;

        var sorted = parameters.SortBy switch
        {
            CaseSortField.CreatedAt => OrderBy(query, c => c.CreatedAt, descending),
            CaseSortField.UpdatedAt => OrderBy(query, c => c.UpdatedAt, descending),
            CaseSortField.Title => OrderBy(query, c => c.Title, descending),
            CaseSortField.OrganizationName => OrderBy(query, c => c.OrganizationName, descending),
            CaseSortField.Status => OrderBy(query, c => c.Status, descending),
            CaseSortField.Priority => OrderBy(query, c => c.Priority, descending),
            _ => throw new ArgumentOutOfRangeException(
                nameof(parameters),
                parameters.SortBy,
                "Unsupported sort field.")
        };

        // Required, not decoration: without a unique tiebreaker, OFFSET/FETCH over a
        // duplicate-heavy column can put the same row on two pages or on none.
        return sorted.ThenBy(c => c.Id);
    }

    private static IOrderedQueryable<Case> OrderBy<TKey>(
        IQueryable<Case> query,
        Expression<Func<Case, TKey>> keySelector,
        bool descending)
    {
        return descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);
    }
}
