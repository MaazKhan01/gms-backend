using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Constants;

/// <summary>One dynamic field a Service asks for. Shape is shared verbatim with
/// the frontend's dynamic form renderer.</summary>
public class ServiceFieldDefinition
{
    /// <summary>Stable machine key used in FieldValuesJson, e.g. "loungeName".</summary>
    public string Key { get; set; }
    public string Label { get; set; }
    public string LabelAr { get; set; }
    /// <summary>One of <see cref="ServiceFieldTypes"/>.</summary>
    public string Type { get; set; } = ServiceFieldTypes.Text;
    public bool Required { get; set; }
    /// <summary>Only meaningful for Type == "select".</summary>
    public List<string> Options { get; set; } = new();
}

public static class ServiceFieldTypes
{
    public const string Text = "text";
    public const string TextArea = "textarea";
    public const string Number = "number";
    public const string Date = "date";
    public const string Select = "select";
    public const string Checkbox = "checkbox";

    public static readonly string[] All = { Text, TextArea, Number, Date, Select, Checkbox };

    public static bool IsValid(string type) => All.Contains(type);
}

/// <summary>
/// Parse/serialize helpers for <c>Service.FieldsSchema</c> and
/// <c>ServiceLevelService.FieldValuesJson</c>. Mirrors the defensive style of
/// <see cref="GuestServices"/>: never throws, never returns null, and treats
/// malformed/legacy JSON as "no fields" rather than failing a request.
/// </summary>
public static class ServiceFieldSchema
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Field definitions, dropping any entry without a usable key and
    /// normalising unknown types to "text".</summary>
    public static List<ServiceFieldDefinition> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<ServiceFieldDefinition>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<ServiceFieldDefinition>>(json, Opts)
                         ?? new List<ServiceFieldDefinition>();

            return parsed
                .Where(f => !string.IsNullOrWhiteSpace(f?.Key))
                .GroupBy(f => f.Key.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var f = g.First();
                    f.Key = f.Key.Trim();
                    if (!ServiceFieldTypes.IsValid(f.Type)) f.Type = ServiceFieldTypes.Text;
                    f.Options ??= new List<string>();
                    return f;
                })
                .ToList();
        }
        catch (JsonException)
        {
            return new List<ServiceFieldDefinition>();
        }
    }

    /// <summary>JSON for storage — null when there are no fields, so the column
    /// stays empty rather than holding "[]".</summary>
    public static string Serialize(IEnumerable<ServiceFieldDefinition> fields)
    {
        var clean = (fields ?? Enumerable.Empty<ServiceFieldDefinition>())
            .Where(f => !string.IsNullOrWhiteSpace(f?.Key))
            .ToList();

        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean, Opts);
    }

    /// <summary>Field values as a flat dictionary. Unknown/blank keys dropped.</summary>
    public static Dictionary<string, string> ParseValues(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Opts)
                       ?.Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                       .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    public static string SerializeValues(Dictionary<string, string> values)
    {
        var clean = (values ?? new Dictionary<string, string>())
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return clean.Count == 0 ? null : JsonSerializer.Serialize(clean, Opts);
    }

    /// <summary>Validates supplied values against a service's schema. Returns the
    /// first problem found, or null when everything required is present.</summary>
    public static string ValidateValues(string fieldsSchema, Dictionary<string, string> values)
    {
        var fields = Parse(fieldsSchema);
        if (fields.Count == 0) return null;

        var supplied = values ?? new Dictionary<string, string>();
        foreach (var f in fields.Where(f => f.Required))
        {
            supplied.TryGetValue(f.Key, out var v);
            if (string.IsNullOrWhiteSpace(v))
                return $"\"{f.Label ?? f.Key}\" is required.";
        }

        foreach (var f in fields.Where(f => f.Type == ServiceFieldTypes.Select && f.Options.Count > 0))
        {
            if (supplied.TryGetValue(f.Key, out var v) && !string.IsNullOrWhiteSpace(v)
                && !f.Options.Contains(v, StringComparer.OrdinalIgnoreCase))
                return $"\"{v}\" is not a valid option for \"{f.Label ?? f.Key}\".";
        }

        return null;
    }
}
