namespace CaseManagement.Api.Tests.Fixtures;

/// <summary>A clock that never moves, so an asserted timestamp is an equality and not a range.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
