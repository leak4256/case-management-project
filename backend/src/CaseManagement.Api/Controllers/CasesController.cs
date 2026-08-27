using CaseManagement.Api.Extensions;
using CaseManagement.Application.DTOs;
using CaseManagement.Application.Interfaces;
using CaseManagement.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace CaseManagement.Api.Controllers;

[ApiController]
[Route("api/cases")]
public class CasesController(ICaseService caseService) : ControllerBase
{
    /// <summary>Returns one page of cases matching the given filters.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<CaseDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CaseDto>>> GetCases(
        [FromQuery] CaseQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await caseService.GetCasesAsync(parameters, cancellationToken);

        return Ok(result);
    }

    /// <summary>Returns aggregate figures for the cases matching the given filters.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<CaseSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CaseSummaryDto>> GetSummary(
        [FromQuery] CaseQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var summary = await caseService.GetSummaryAsync(parameters, cancellationToken);

        return Ok(summary);
    }

    /// <summary>Returns one case, carrying its concurrency token in the ETag header.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<CaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseDto>> GetCase(int id, CancellationToken cancellationToken)
    {
        var result = await caseService.GetCaseAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = result.RowVersion.ToETag();

        return Ok(result.Case);
    }

    /// <summary>
    /// Changes the status of one case. The caller must echo the ETag from a previous read in
    /// <c>If-Match</c>; the update is rejected with 409 if the case changed in the meantime.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<CaseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<ActionResult<CaseDto>> UpdateStatus(
        int id,
        [FromBody] UpdateCaseStatusRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                // Only well-known status codes have a mapping; 428 is not one of them.
                type: "https://tools.ietf.org/html/rfc6585#section-3",
                title: "If-Match header is required.",
                detail: "Read the case first and send the ETag it returned in If-Match.");
        }

        // A list of candidates arrives comma-joined and fails to parse, which is the intent: this
        // endpoint targets exactly the one version the caller read.
        if (!ETagExtensions.TryParseRowVersion(ifMatch, out var rowVersion))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "If-Match header is malformed.",
                detail: "If-Match must carry exactly one strong ETag as returned by a previous read.");
        }

        var outcome = await caseService.UpdateStatusAsync(
            id,
            request.Status!.Value,
            rowVersion,
            cancellationToken);

        if (outcome.Result == UpdateCaseStatusResult.NotFound)
        {
            return NotFound();
        }

        var current = outcome.Current!;
        Response.Headers.ETag = current.RowVersion.ToETag();

        return outcome.Result == UpdateCaseStatusResult.Updated
            ? Ok(current.Case)
            : Conflict(BuildConflictProblem(current));
    }

    private ProblemDetails BuildConflictProblem(CaseWithVersion current)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            StatusCodes.Status409Conflict,
            title: "The case was modified by someone else.",
            detail: "Reload the case and reapply the change to the current version.");

        // The current state travels in the body as well as the header, so a client that cannot
        // read cross-origin headers can still recover without a second request.
        problem.Extensions["currentStatus"] = current.Case.Status;
        problem.Extensions["currentETag"] = current.RowVersion.ToETag();
        problem.Extensions["currentRowVersion"] = current.RowVersion;
        problem.Extensions["currentUpdatedAt"] = current.Case.UpdatedAt;

        return problem;
    }
}
