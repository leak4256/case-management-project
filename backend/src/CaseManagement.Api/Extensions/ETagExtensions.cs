using System.Diagnostics.CodeAnalysis;

namespace CaseManagement.Api.Extensions;

public static class ETagExtensions
{
    private const int RowVersionLength = 8;

    public static string ToETag(this byte[] rowVersion)
    {
        return $"\"{Convert.ToBase64String(rowVersion)}\"";
    }

    public static bool TryParseRowVersion(string? eTag, [NotNullWhen(true)] out byte[]? rowVersion)
    {
        rowVersion = null;

        var value = eTag?.Trim();

        // Quotes are part of the entity-tag grammar, so an unquoted value is malformed. This also
        // rejects the weak form (W/"..."), which If-Match does not accept.
        if (value is not ['"', .., '"'])
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[RowVersionLength];

        if (!Convert.TryFromBase64Chars(value.AsSpan(1, value.Length - 2), buffer, out var written)
            || written != RowVersionLength)
        {
            return false;
        }

        rowVersion = buffer.ToArray();

        return true;
    }
}
