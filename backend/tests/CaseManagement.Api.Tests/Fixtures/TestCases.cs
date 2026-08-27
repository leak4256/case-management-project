using CaseManagement.Domain.Entities;
using CaseManagement.Domain.Enums;

namespace CaseManagement.Api.Tests.Fixtures;

/// <summary>Twelve query cases for the read tests, and the sandbox cases the update tests may change.</summary>
internal static class TestCases
{
    // An assertion about status counts has to scope its query by organization, or an update test
    // moves the expected number underneath it.
    public const string SandboxOrganization = "Zeta Mutation Sandbox";

    public const int QueryCaseCount = 12;

    public static readonly int TotalCount = QueryCaseCount + Sandbox.All.Length;

    /// <summary>The oldest query case. Sandbox cases are dated before it, so they sort last.</summary>
    public static readonly DateTime FirstQueryCreatedAt =
        new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>One row per update scenario, so no two tests compete over the same case.</summary>
    public static class Sandbox
    {
        public const string SuccessfulUpdate = "Sandbox row for the successful status update";

        public const string StaleETag = "Sandbox row for the stale ETag conflict";

        public const string RepeatedUpdate = "Sandbox row for the repeated identical update";

        public const string CompetingWriter = "Sandbox row for the competing writer";

        public const string SummaryRefresh = "Sandbox row for the summary refresh";

        public const string ListedVersion = "Sandbox row for the version taken from the list";

        public static readonly string[] All =
        [
            SuccessfulUpdate,
            StaleETag,
            RepeatedUpdate,
            CompetingWriter,
            SummaryRefresh,
            ListedVersion
        ];
    }

    public static IReadOnlyList<Case> Build() => [.. BuildSandboxCases(), .. BuildQueryCases()];

    private static IEnumerable<Case> BuildQueryCases()
    {
        var definitions = new[]
        {
            ("Invoice discrepancy on order 1001", "Northwind Traders", CaseStatus.New, CasePriority.High),
            ("Refund request for order 1002", "Northwind Traders", CaseStatus.InProgress, CasePriority.High),
            ("Contract renewal enquiry", "Contoso Ltd", CaseStatus.Waiting, CasePriority.Medium),
            ("Access permissions review", "Contoso Ltd", CaseStatus.Completed, CasePriority.Low),
            ("Service outage reported at branch 12", "Fabrikam Inc", CaseStatus.New, CasePriority.Medium),
            ("Data export request", "Fabrikam Inc", CaseStatus.InProgress, CasePriority.Low),
            ("Bulk user import failed", "Adventure Works", CaseStatus.Waiting, CasePriority.High),
            ("Delivery delay for shipment 77", "Adventure Works", CaseStatus.Completed, CasePriority.Medium),
            ("Password reset for administrator account", "Tailspin Toys", CaseStatus.New, CasePriority.Low),
            ("Escalation: unresolved ticket 44", "Tailspin Toys", CaseStatus.InProgress, CasePriority.Medium),
            ("Missing documentation for audit", "Wide World Importers", CaseStatus.Waiting, CasePriority.Low),
            ("Change of primary contact person", "Wide World Importers", CaseStatus.New, CasePriority.High)
        };

        return definitions.Select((definition, index) =>
        {
            var (title, organization, status, priority) = definition;

            return CreateAt(FirstQueryCreatedAt.AddDays(index), title, organization, status, priority);
        });
    }

    private static IEnumerable<Case> BuildSandboxCases()
    {
        return Sandbox.All.Select((title, index) => CreateAt(
            FirstQueryCreatedAt.AddDays(index - Sandbox.All.Length),
            title,
            SandboxOrganization,
            CaseStatus.New,
            CasePriority.Medium));
    }

    private static Case CreateAt(
        DateTime createdAt,
        string title,
        string organization,
        CaseStatus status,
        CasePriority priority)
    {
        return Case.Create(title, organization, status, priority, createdAt, createdAt);
    }
}
