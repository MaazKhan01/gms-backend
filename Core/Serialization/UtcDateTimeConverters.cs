using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Serialization;

// Every DateTime this API writes is UTC (DateTime.UtcNow, or a DB column
// defaulted to sysutcdatetime()) — but SQL Server's datetime2 has no timezone
// concept, so EF Core always hands a stored value back with Kind=Unspecified,
// never Kind=Utc. System.Text.Json's default DateTime writer only appends a
// "Z" suffix when Kind is Utc; for Unspecified it writes a bare timestamp.
// Browsers then parse that bare string as LOCAL time (per the ECMAScript
// date-parsing spec), silently shifting every timestamp in the app by the
// client's UTC offset. Forcing Kind=Utc immediately before writing is the fix
// — applied only on the way out; reads are untouched.
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    // One shape for every timestamp the API writes: 2026-12-16T00:00:00Z.
    // Seconds precision, nothing after them — the default writer would append
    // fractional seconds whenever a value happened to carry them, so the same
    // field came back in two different shapes depending on the row.
    internal const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    internal static string ToUtcString(DateTime value)
        => (value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc))
            .ToString(Format, CultureInfo.InvariantCulture);

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToUtcString(value));
}

public class UtcNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) { writer.WriteNullValue(); return; }
        writer.WriteStringValue(UtcDateTimeConverter.ToUtcString(value.Value));
    }
}
