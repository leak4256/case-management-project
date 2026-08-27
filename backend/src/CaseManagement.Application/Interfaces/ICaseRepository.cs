using CaseManagement.Application.DTOs;
using CaseManagement.Application.Queries;
using CaseManagement.Domain.Enums;

namespace CaseManagement.Application.Interfaces;

public interface ICaseRepository
{
    Task<PagedResult<CaseDto>> GetPagedAsync(
        CaseQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<CaseSummaryDto> GetSummaryAsync(
        CaseQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<CaseWithVersion?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<UpdateCaseStatusOutcome> UpdateStatusAsync(
        int id,
        CaseStatus status,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default);
}
