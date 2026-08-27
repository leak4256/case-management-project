using CaseManagement.Application.DTOs;
using CaseManagement.Application.Queries;
using CaseManagement.Domain.Enums;

namespace CaseManagement.Application.Interfaces;

public interface ICaseService
{
    Task<PagedResult<CaseDto>> GetCasesAsync(
        CaseQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<CaseSummaryDto> GetSummaryAsync(
        CaseQueryParameters parameters,
        CancellationToken cancellationToken = default);

    Task<CaseWithVersion?> GetCaseAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<UpdateCaseStatusOutcome> UpdateStatusAsync(
        int id,
        CaseStatus status,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken = default);
}
