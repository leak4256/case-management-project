using CaseManagement.Domain.Enums;

namespace CaseManagement.Application.DTOs;

public sealed record CaseDto
{
    public required int Id { get; init; }

    public required string Title { get; init; }

    public required string OrganizationName { get; init; }

    public required CaseStatus Status { get; init; }

    public required CasePriority Priority { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }

    /// <summary>The concurrency token to echo back in <c>If-Match</c> when changing the status.</summary>
    public required byte[] RowVersion { get; init; }
}
