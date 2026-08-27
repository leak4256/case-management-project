using System.ComponentModel.DataAnnotations;
using CaseManagement.Domain.Enums;

namespace CaseManagement.Application.Queries;

public class CaseQueryParameters : IValidatableObject
{
    public const int MaxPageSize = 100;

    public const int DefaultPageSize = 25;

    [MaxLength(200, ErrorMessage = "Search text cannot exceed 200 characters.")]
    public string? Search { get; set; }

    public CaseStatus[]? Status { get; set; }

    public CasePriority[]? Priority { get; set; }

    [MaxLength(150, ErrorMessage = "Organization name cannot exceed 150 characters.")]
    public string? Organization { get; set; }

    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    public CaseSortField SortBy { get; set; } = CaseSortField.CreatedAt;

    public SortDirection SortDirection { get; set; } = SortDirection.Descending;

    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = DefaultPageSize;

    // Enum binding accepts any number, defined or not: ?sortBy=99 binds without a model error and
    // only a name like ?sortBy=Bogus fails on its own. Left unchecked, an undefined sort field
    // throws in the repository and an undefined status silently filters everything out.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CreatedFrom.HasValue && CreatedTo.HasValue && CreatedFrom > CreatedTo)
        {
            yield return new ValidationResult(
                "CreatedFrom must be earlier than or equal to CreatedTo.",
                [nameof(CreatedFrom), nameof(CreatedTo)]);
        }

        if (!Enum.IsDefined(SortBy))
        {
            yield return new ValidationResult(
                "SortBy must be one of the supported sort fields.",
                [nameof(SortBy)]);
        }

        if (!Enum.IsDefined(SortDirection))
        {
            yield return new ValidationResult(
                "SortDirection must be either Ascending or Descending.",
                [nameof(SortDirection)]);
        }

        if (HasUndefinedValue(Status))
        {
            yield return new ValidationResult(
                "Status contains a value that is not a known case status.",
                [nameof(Status)]);
        }

        if (HasUndefinedValue(Priority))
        {
            yield return new ValidationResult(
                "Priority contains a value that is not a known case priority.",
                [nameof(Priority)]);
        }
    }

    private static bool HasUndefinedValue<TEnum>(TEnum[]? values)
        where TEnum : struct, Enum
    {
        return values is not null && Array.Exists(values, value => !Enum.IsDefined(value));
    }
}
