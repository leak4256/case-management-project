namespace CaseManagement.Domain.Enums;

// Persisted as tinyint — these numbers must never be reassigned.
public enum CaseStatus : byte
{
    New = 1,
    InProgress = 2,
    Waiting = 3,
    Completed = 4
}
