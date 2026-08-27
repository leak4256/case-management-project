namespace CaseManagement.Application.DTOs;

public sealed record CaseSummaryDto
{
    public required int TotalCount { get; init; }

    public required int NewCount { get; init; }

    public required int InProgressCount { get; init; }

    public required int WaitingCount { get; init; }

    public required int CompletedCount { get; init; }

    public required int LowPriorityCount { get; init; }

    public required int MediumPriorityCount { get; init; }

    public required int HighPriorityCount { get; init; }

    // Null rather than zero when no case is open: the mean of an empty set is not a number.
    public required double? AverageOpenAgeInDays { get; init; }

    public required int UpdatedInLastSevenDays { get; init; }
}
