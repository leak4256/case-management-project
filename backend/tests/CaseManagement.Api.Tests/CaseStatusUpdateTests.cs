using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CaseManagement.Api.Extensions;
using CaseManagement.Api.Tests.Fixtures;
using CaseManagement.Application.DTOs;
using CaseManagement.Application.Queries;
using CaseManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CaseManagement.Api.Tests;

[Collection(ApiCollection.Name)]
public class CaseStatusUpdateTests(DatabaseFixture fixture)
{
    // Well-formed but belonging to no row: enough to get past the header checks.
    private const string UnusedETag = "\"AAAAAAAAB9E=\"";

    [Fact]
    public async Task Updating_with_the_current_etag_changes_the_case()
    {
        using var client = fixture.CreateClient();
        var id = await FindCaseIdAsync(TestCases.Sandbox.SuccessfulUpdate);

        var (eTag, before) = await ReadCaseAsync(client, id);

        var response = await client.SendAsync(
            PatchStatus(id, CaseStatus.InProgress, eTag));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await response.Content.ReadFromJsonAsync<CaseDto>(TestJson.Options);

        Assert.Equal(CaseStatus.InProgress, after!.Status);
        Assert.Equal(before.CreatedAt, after.CreatedAt);

        // The server stamps UpdatedAt from its own injected clock — not from the request, and not
        // from the database's clock either.
        Assert.True(after.UpdatedAt > before.UpdatedAt);
        Assert.Equal(ApiFactory.UtcNow, after.UpdatedAt);

        // A new version has to reach the caller, or the next update of the same case is refused.
        Assert.NotNull(response.Headers.ETag);
        Assert.NotEqual(eTag, response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task Updating_with_a_stale_etag_returns_409_and_the_current_state()
    {
        using var client = fixture.CreateClient();
        var id = await FindCaseIdAsync(TestCases.Sandbox.StaleETag);

        var (staleETag, _) = await ReadCaseAsync(client, id);

        var accepted = await client.SendAsync(PatchStatus(id, CaseStatus.Waiting, staleETag));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var rejected = await client.SendAsync(PatchStatus(id, CaseStatus.Completed, staleETag));

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);
        Assert.NotNull(problem);

        // The whole point of the 409: the client can recover from the body alone.
        Assert.Equal(nameof(CaseStatus.Waiting), Extension(problem, "currentStatus"));
        Assert.Equal(accepted.Headers.ETag!.ToString(), Extension(problem, "currentETag"));
        Assert.NotNull(Extension(problem, "currentUpdatedAt"));

        var (currentETag, current) = await ReadCaseAsync(client, id);
        Assert.Equal(CaseStatus.Waiting, current.Status);
        Assert.Equal(currentETag, Extension(problem, "currentETag"));
    }

    // The browser never reads a case before editing it; it sends the version the row was listed
    // with. Every other test here takes its token from the ETag header instead, so without this
    // one the list could stop carrying a usable version and nothing would fail.
    [Fact]
    public async Task A_version_taken_from_the_list_updates_once_and_then_conflicts()
    {
        using var client = fixture.CreateClient();

        var listed = await ReadListedCaseAsync(client, TestCases.Sandbox.ListedVersion);
        var shownToBothTabs = listed.RowVersion.ToETag();

        var accepted = await client.SendAsync(
            PatchStatus(listed.Id, CaseStatus.InProgress, shownToBothTabs));

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        var rejected = await client.SendAsync(
            PatchStatus(listed.Id, CaseStatus.Completed, shownToBothTabs));

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);
        Assert.NotNull(problem);

        // The second tab re-arms its row from this, so a retry must not be stale again.
        var currentRowVersion = Extension(problem, "currentRowVersion");
        Assert.NotNull(currentRowVersion);

        var retried = await client.SendAsync(
            PatchStatus(listed.Id, CaseStatus.Completed, $"\"{currentRowVersion}\""));

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    [Fact]
    public async Task A_competing_writer_outside_the_api_also_produces_409()
    {
        using var client = fixture.CreateClient();
        var id = await FindCaseIdAsync(TestCases.Sandbox.CompetingWriter);

        var (eTag, _) = await ReadCaseAsync(client, id);

        // A second connection the API knows nothing about — the real conflict, not a client
        // replaying its own request.
        await using (var dbContext = fixture.CreateDbContext())
        {
            await dbContext.Database.ExecuteSqlAsync(
                $"UPDATE Cases SET UpdatedAt = DATEADD(second, 1, UpdatedAt) WHERE Id = {id}");
        }

        var response = await client.SendAsync(PatchStatus(id, CaseStatus.Completed, eTag));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);
        Assert.NotNull(problem);

        // The foreign write touched UpdatedAt only, so the status the client tried to replace
        // is still the one it read.
        Assert.Equal(nameof(CaseStatus.New), Extension(problem, "currentStatus"));
        Assert.NotEqual(eTag, Extension(problem, "currentETag"));
    }

    [Fact]
    public async Task Repeating_the_same_status_is_idempotent_but_still_version_checked()
    {
        using var client = fixture.CreateClient();
        var id = await FindCaseIdAsync(TestCases.Sandbox.RepeatedUpdate);

        var (firstETag, _) = await ReadCaseAsync(client, id);

        var changed = await client.SendAsync(PatchStatus(id, CaseStatus.InProgress, firstETag));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var currentETag = changed.Headers.ETag!.ToString();

        var repeated = await client.SendAsync(PatchStatus(id, CaseStatus.InProgress, currentETag));

        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(currentETag, repeated.Headers.ETag!.ToString());

        // No UPDATE runs when the status already matches, so SQL Server never compares the
        // versions — without the explicit check a stale caller would be told it succeeded.
        var stale = await client.SendAsync(PatchStatus(id, CaseStatus.InProgress, firstETag));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Updating_a_case_that_does_not_exist_returns_404()
    {
        using var client = fixture.CreateClient();

        var response = await client.SendAsync(
            PatchStatus(999_999, CaseStatus.Completed, UnusedETag));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Updating_without_if_match_returns_428()
    {
        using var client = fixture.CreateClient();
        var id = await FindCaseIdAsync(TestCases.Sandbox.SuccessfulUpdate);

        var response = await client.SendAsync(PatchStatus(id, CaseStatus.Completed, ifMatch: null));

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);

        Assert.Equal("If-Match header is required.", problem!.Title);
    }

    [Fact]
    public async Task A_malformed_if_match_returns_400()
    {
        using var client = fixture.CreateClient();
        var id = await FindCaseIdAsync(TestCases.Sandbox.SuccessfulUpdate);

        // Valid base64, but three bytes where a rowversion is eight: the parser checks the
        // decoded length and not merely the encoding.
        var response = await client.SendAsync(PatchStatus(id, CaseStatus.Completed, "\"AAAA\""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options);

        Assert.Equal("If-Match header is malformed.", problem!.Title);
    }

    [Fact]
    public async Task A_status_outside_the_enum_returns_400()
    {
        using var client = fixture.CreateClient();
        var id = await FindCaseIdAsync(TestCases.Sandbox.SuccessfulUpdate);

        // A number is the case the DTO was shaped for: the JSON converter accepts it happily and
        // only EnumDataType stops it. A misspelled name fails earlier, in the deserializer.
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/cases/{id}/status")
        {
            Content = new StringContent("{\"status\":99}", Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", UnusedETag);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>(TestJson.Options);

        Assert.Contains(nameof(UpdateCaseStatusRequest.Status), problem!.Errors.Keys);
    }

    [Fact]
    public async Task The_summary_reflects_a_status_change()
    {
        using var client = fixture.CreateClient();
        var id = await FindCaseIdAsync(TestCases.Sandbox.SummaryRefresh);

        // Scoped to this one row, so the other update tests cannot move the figures.
        var query = "/api/cases/summary?search="
            + Uri.EscapeDataString(TestCases.Sandbox.SummaryRefresh);

        var before = await client.GetFromJsonAsync<CaseSummaryDto>(query, TestJson.Options);

        Assert.Equal(1, before!.NewCount);
        Assert.Equal(0, before.CompletedCount);
        Assert.NotNull(before.AverageOpenAgeInDays);
        Assert.Equal(0, before.UpdatedInLastSevenDays);

        var (eTag, _) = await ReadCaseAsync(client, id);

        var response = await client.SendAsync(PatchStatus(id, CaseStatus.Completed, eTag));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await client.GetFromJsonAsync<CaseSummaryDto>(query, TestJson.Options);

        Assert.Equal(0, after!.NewCount);
        Assert.Equal(1, after.CompletedCount);

        // Completed rows are excluded from the mean, and this row is the whole filtered set.
        Assert.Null(after.AverageOpenAgeInDays);
        Assert.Equal(1, after.UpdatedInLastSevenDays);
    }

    private static HttpRequestMessage PatchStatus(int id, CaseStatus status, string? ifMatch)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/cases/{id}/status")
        {
            Content = new StringContent(
                $$"""{"status":"{{status}}"}""",
                Encoding.UTF8,
                "application/json")
        };

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return request;
    }

    private static async Task<CaseDto> ReadListedCaseAsync(HttpClient client, string title)
    {
        var response = await client.GetAsync(
            $"/api/cases?organization={Uri.EscapeDataString(TestCases.SandboxOrganization)}&pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content
            .ReadFromJsonAsync<PagedResult<CaseDto>>(TestJson.Options);

        return page!.Items.Single(c => c.Title == title);
    }

    private static async Task<(string ETag, CaseDto Case)> ReadCaseAsync(HttpClient client, int id)
    {
        var response = await client.GetAsync($"/api/cases/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);

        var dto = await response.Content.ReadFromJsonAsync<CaseDto>(TestJson.Options);

        return (response.Headers.ETag.ToString(), dto!);
    }

    private static string? Extension(ProblemDetails problem, string name)
    {
        return problem.Extensions[name] is JsonElement element ? element.ToString() : null;
    }

    private async Task<int> FindCaseIdAsync(string title)
    {
        await using var dbContext = fixture.CreateDbContext();

        return await dbContext.Cases
            .Where(c => c.Title == title)
            .Select(c => c.Id)
            .SingleAsync();
    }
}
