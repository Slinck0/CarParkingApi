using System.Text.Json;
using System.Text.Json.Serialization;

namespace V2.Import;
using System.Diagnostics.CodeAnalysis;
[ExcludeFromCodeCoverage]
public sealed class BoolConverter : JsonConverter<bool?>
{
    public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:  return true;
            case JsonTokenType.False: return false;
            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var n)) return n != 0;
                // fallback: lees als double en check != 0
                if (reader.TryGetDouble(out var d)) return Math.Abs(d) > double.Epsilon;
                return null;
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;
                s = s.Trim();
                if (bool.TryParse(s, out var b)) return b;
                if (s == "1" || s.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
                if (s == "0" || s.Equals("no",  StringComparison.OrdinalIgnoreCase)) return false;
                // ook "y"/"n"
                if (s.Equals("y", StringComparison.OrdinalIgnoreCase)) return true;
                if (s.Equals("n", StringComparison.OrdinalIgnoreCase)) return false;
                return null;
            case JsonTokenType.Null:
                return null;
            default:
                throw new JsonException($"Unsupported token for bool: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
    {
        if (value.HasValue) writer.WriteBooleanValue(value.Value);
        else writer.WriteNullValue();
    }
}