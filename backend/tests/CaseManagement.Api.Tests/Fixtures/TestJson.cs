using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaseManagement.Api.Tests.Fixtures;

/// <summary>Mirrors the API's serializer — camelCase names and enums written by name.</summary>
internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
