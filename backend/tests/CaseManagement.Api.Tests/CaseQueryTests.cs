using System.Net;
using System.Net.Http.Json;
using CaseManagement.Api.Tests.Fixtures;
using CaseManagement.Application.DTOs;
using CaseManagement.Application.Queries;
using CaseManagement.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace CaseManagement.Api.Tests;

[Collection(ApiCollection.Name)]
public class CaseQueryTests(DatabaseFixture fixture)
{
    // The sandbox rows the update tests mutate are all dated before the query cases, so starting
    // every query at midnight of the first one keeps their status changes out of these counts.
    private static readonly string QueryWindow = "createdFrom=" + Uri.EscapeDataString(
        TestCases.FirstQueryCreatedAt.Date.ToString("O"));

    [Fact]
    public async Task Filtering_by_status_and_priority_returns_only_matching_cases()
    {
        var page = await GetPageAsync("status=InProgress&priority=High&priority=Medium&pageSize=100");

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(
            new[] { "Escalation: unresolved ticket 44", "Refund request for order 1002" },
            page.Items.Select(c => c.Title).Order());
        Assert.All(page.Items, c => Assert.Equal(CaseStatus.InProgress, c.Status));
        Assert.All(page.Items, c => Assert.NotEqual(CasePriority.Low, c.Priority));
    }

    [Fact]
    public async Task Every_listed_case_carries_the_version_a_status_update_asserts_against()
    {
        var page = await GetPageAsync("pageSize=5");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, c => Assert.Equal(8, c.RowVersion.Length));
    }

    [Fact]
    public async Task Filtering_by_organization_matches_on_the_prefix_only()
    {
        var page = await GetPageAsync("organization=Northwind&pageSize=100");

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, c => Assert.Equal("Northwind Traders", c.OrganizationName));
    }

    [Fact]
    public async Task Paging_walks_the_result_set_without_overlap_or_gaps()
    {
        var pages = new[]
        {
            await GetPageAsync("page=1&pageSize=5"),
            await GetPageAsync("page=2&pageSize=5"),
            await GetPageAsync("page=3&pageSize=5")
        };

        Assert.All(pages, p => Assert.Equal(TestCases.QueryCaseCount, p.TotalCount));
        Assert.Equal(new[] { 5, 5, 2 }, pages.Select(p => p.Items.Count));
        Assert.Equal(3, pages[0].TotalPages);

        var ids = pages.SelectMany(p => p.Items).Select(c => c.Id).ToArray();
        Assert.Equal(ids.Order(), ids.Distinct().Order());

        Assert.True(pages[0].HasNextPage);
        Assert.False(pages[^1].HasNextPage);
    }

    [Fact]
    public async Task Page_beyond_the_last_returns_no_items_but_the_real_total()
    {
        var page = await GetPageAsync("page=4&pageSize=5");

        Assert.Empty(page.Items);
        Assert.Equal(TestCases.QueryCaseCount, page.TotalCount);
    }

    [Fact]
    public async Task Sorting_by_created_at_descending_returns_the_newest_first()
    {
        var page = await GetPageAsync("sortBy=CreatedAt&sortDirection=Descending&pageSize=100");

        var createdAt = page.Items.Select(c => c.CreatedAt).ToArray();

        Assert.Equal(createdAt.OrderByDescending(value => value), createdAt);
        Assert.Equal("Change of primary contact person", page.Items[0].Title);
        Assert.Equal("Invoice discrepancy on order 1001", page.Items[^1].Title);
    }

    [Fact]
    public async Task Search_matches_title_and_organization_ignoring_case()
    {
        // Mixed case against "Contoso" and "contact": one spelling proves the collation is
        // case-insensitive, three spellings prove it three times.
        var page = await GetPageAsync("search=CoNt&pageSize=100");

        // "Contoso Ltd" matches on the organization, "contact" on the title.
        Assert.Equal(
            new[]
            {
                "Access permissions review",
                "Change of primary contact person",
                "Contract renewal enquiry"
            },
            page.Items.Select(c => c.Title).Order());
    }

    [Fact]
    public async Task Page_size_above_the_ceiling_is_rejected()
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync(
            $"/api/cases?pageSize={CaseQueryParameters.MaxPageSize + 1}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ValidationProblemDetails>(TestJson.Options);

        Assert.Contains("PageSize", problem!.Errors.Keys);
    }

    private async Task<PagedResult<CaseDto>> GetPageAsync(string query)
    {
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/cases?{QueryWindow}&{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content
            .ReadFromJsonAsync<PagedResult<CaseDto>>(TestJson.Options))!;
    }
}
