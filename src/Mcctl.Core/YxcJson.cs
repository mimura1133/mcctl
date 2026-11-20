using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mcctl;

/// <summary>Shared JSON settings for the YXC wire format (snake_case, lenient numbers).</summary>
public static class YxcJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
