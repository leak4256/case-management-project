using System.ComponentModel.DataAnnotations;
using CaseManagement.Domain.Enums;

namespace CaseManagement.Application.DTOs;

public sealed record UpdateCaseStatusRequest
{
    // Nullable so that a missing "status" fails as Required instead of binding silently to New,
    // and EnumDataType so that a number outside the enum is rejected rather than stored.
    [Required(ErrorMessage = "Status is required.")]
    [EnumDataType(typeof(CaseStatus), ErrorMessage = "Status must be a known case status.")]
    public CaseStatus? Status { get; init; }
}
