using System.Net;
using CaseManagement.Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace CaseManagement.Api.Tests;

[Collection(ApiCollection.Name)]
public class SmokeTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Get_health_returns_200()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Database_holds_only_the_deterministic_test_seed()
    {
        await using var dbContext = fixture.CreateDbContext();

        Assert.Equal(TestCases.TotalCount, await dbContext.Cases.CountAsync());
    }
}
