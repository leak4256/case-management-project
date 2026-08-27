using CaseManagement.Domain.Enums;

namespace CaseManagement.Domain.Entities;

public class Case
{
    private Case()
    {
    }

    public int Id { get; private set; }

    public string Title { get; private set; } = null!;

    public string OrganizationName { get; private set; } = null!;

    public CaseStatus Status { get; private set; }

    public CasePriority Priority { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>SQL Server rowversion, used by EF Core as the optimistic concurrency token.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public static Case Create(
        string title,
        string organizationName,
        CaseStatus status,
        CasePriority priority,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        return new Case
        {
            Title = title,
            OrganizationName = organizationName,
            Status = status,
            Priority = priority,
            CreatedAt = createdAtUtc,
            UpdatedAt = updatedAtUtc
        };
    }

    /// <summary>Applies the new status, and reports whether anything actually changed.</summary>
    public bool ChangeStatus(CaseStatus newStatus, DateTime utcNow)
    {
        // Deliberate no-op: EF then issues no UPDATE and RowVersion stays put, making a repeated
        // request idempotent. The caller needs to know, because no UPDATE also means no
        // concurrency check.
        if (Status == newStatus)
        {
            return false;
        }

        Status = newStatus;
        UpdatedAt = utcNow;

        return true;
    }
}
