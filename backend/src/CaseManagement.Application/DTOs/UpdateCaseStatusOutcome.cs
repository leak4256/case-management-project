namespace CaseManagement.Application.DTOs;

public enum UpdateCaseStatusResult
{
    Updated,
    NotFound,
    VersionMismatch
}

public sealed record UpdateCaseStatusOutcome
{
    public required UpdateCaseStatusResult Result { get; init; }

    /// <summary>The stored case: the updated one on success, the server's copy on a mismatch.</summary>
    public CaseWithVersion? Current { get; init; }
}
