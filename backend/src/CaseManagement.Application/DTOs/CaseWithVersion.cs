namespace CaseManagement.Application.DTOs;

public sealed record CaseWithVersion
{
    public required CaseDto Case { get; init; }

    public required byte[] RowVersion { get; init; }
}
