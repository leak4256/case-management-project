using Bogus;
using CaseManagement.Domain.Entities;
using CaseManagement.Domain.Enums;

namespace CaseManagement.Infrastructure.Seeding;

internal static class CaseFaker
{
    private const int RandomSeed = 20260823;

    private const int OrganizationCount = 180;
    private const int HistoryMonths = 18;

    private static readonly string[] TitleTemplates =
    [
        "Invoice discrepancy on order {0}",
        "Request to update billing address",
        "Service outage reported at branch {0}",
        "Contract renewal enquiry",
        "Refund request for order {0}",
        "Onboarding support for new users",
        "Access permissions review",
        "Delivery delay for shipment {0}",
        "Data export request",
        "Integration failure with external system",
        "Change of primary contact person",
        "Quote request for additional licences",
        "Password reset for administrator account",
        "Report of incorrect tax calculation",
        "Request to cancel subscription",
        "Escalation: unresolved ticket {0}",
        "Missing documentation for audit",
        "Bulk user import failed",
        "Request for account statement",
        "Complaint about response time"
    ];

    public static IReadOnlyList<Case> Generate(int count, DateTime nowUtc)
    {
        Randomizer.Seed = new Random(RandomSeed);

        var organizations = BuildOrganizationPool();
        var earliest = nowUtc.AddMonths(-HistoryMonths);

        var faker = new Faker<Case>().CustomInstantiator(f =>
        {
            var organization = f.Random.WeightedRandom(organizations.Names, organizations.Weights);

            var title = string.Format(f.PickRandom(TitleTemplates), f.Random.Int(10000, 99999));

            var status = f.Random.WeightedRandom(
                [CaseStatus.New, CaseStatus.InProgress, CaseStatus.Waiting, CaseStatus.Completed],
                [0.22f, 0.28f, 0.14f, 0.36f]);

            var priority = f.Random.WeightedRandom(
                [CasePriority.Low, CasePriority.Medium, CasePriority.High],
                [0.34f, 0.46f, 0.20f]);

            var age = Math.Pow(f.Random.Double(), 2);
            var createdAt = earliest.AddTicks((long)((nowUtc - earliest).Ticks * (1 - age)));

            var updatedAt = status == CaseStatus.New
                ? createdAt
                : f.Date.Between(createdAt, nowUtc);

            return Case.Create(title, organization, status, priority, createdAt, updatedAt);
        });

        return faker.Generate(count);
    }

    private static (string[] Names, float[] Weights) BuildOrganizationPool()
    {
        var companyFaker = new Faker();

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (names.Count < OrganizationCount)
        {
            names.Add(companyFaker.Company.CompanyName());
        }

        var nameArray = names.ToArray();

        var weights = new float[nameArray.Length];
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] = 1f / (i + 5);
        }

        // WeightedRandom requires the weights to sum to 1; unnormalised, the first entry wins every
        // draw.
        var total = weights.Sum();
        for (var i = 0; i < weights.Length; i++)
        {
            weights[i] /= total;
        }

        return (nameArray, weights);
    }
}
