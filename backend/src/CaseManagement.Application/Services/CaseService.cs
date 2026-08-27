using CaseManagement.Application.DTOs;
using CaseManagement.Application.Interfaces;
using CaseManagement.Application.Queries;
using CaseManagement.Domain.Enums;

namespace CaseManagement.Application.Services;

public class CaseService(ICaseRepository repository) : ICaseService
{
    public Task<PagedResult<CaseDto>> GetCasesAsync(
        CaseQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        return repository.GetPagedAsync(parameters, cancellationToken);
    }

    public Task<CaseSummaryDto> GetSummaryAsync(
        CaseQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        return repository.GetSummaryAsync(parameters, cancellationToken);
    }

    public Task<CaseWithVersion?> GetCaseAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return repository.GetByIdAsync(id, cancellationToken);
    }

    public Task<UpdateCaseStatusOutcome> UpdateStatusAsync(
        int id,
        CaseStatus status,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        return repository.UpdateStatusAsync(id, status, expectedRowVersion, cancellationToken);
    }
}
