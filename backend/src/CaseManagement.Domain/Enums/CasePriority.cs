namespace CaseManagement.Domain.Enums;

// Ordered low to high so sorting by the stored number sorts by real priority.
// Persisted as tinyint — these numbers must never be reassigned.
public enum CasePriority : byte
{
    Low = 1,
    Medium = 2,
    High = 3
}
