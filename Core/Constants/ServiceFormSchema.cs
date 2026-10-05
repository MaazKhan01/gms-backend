using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Constants;

/// <summary>Field types a service form may use. See docs/service-levels-v2.md.</summary>
public static class ServiceFieldTypes
{
    public const string Text = "text";
    public const string Textarea = "textarea";
    public const string Number = "number";
    public const string Date = "date";
    public const string DateTime = "datetime";
    public const string Time = "time";
    public const string Select = "select";
    public const string Checkbox = "checkbox";

    /// <summary>
    /// Options come from an existing lookup table (airports, hotels, drivers …)
    /// named by the field's SourceKey. The stored value is the row's PublicId.
    /// </summary>
    public const string Lookup = "lookup";

    /// <summary>
    /// An uploaded document — a visa, a ticket, a signed letter. The stored
    /// value is the blob URL, exactly like every other attachment field in the
    /// system (Event.AttachmentUrl, HostInvitation.AttachmentUrl): the client
    /// uploads first and saves the URL it gets back, and BlobSasMiddleware
    /// re-signs it on read. Nothing about the file itself lives in this column.
    /// </summary>
    public const string File = "file";

    public static readonly string[] All =
        { Text, Textarea, Number, Date, DateTime, Time, Select, Checkbox, Lookup, File };

    public static bool IsValid(string t) => All.Contains(t);
}

public class ServiceFieldOption
{
    public string Value { get; set; }
    public string Label { get; set; }
    public string LabelAr { get; set; }
}

public class ServiceFieldDefinition
{
    public string Key { get; set; }
    public string Label { get; set; }
    public string LabelAr { get; set; }
    public string Type { get; set; } = ServiceFieldTypes.Text;
    public bool Required { get; set; }
    public string Placeholder { get; set; }
    public string HelpText { get; set; }

    /// <summary>Only for type "select" — hand-authored options.</summary>
    public List<ServiceFieldOption> Options { get; set; } = new();

    /// <summary>
    /// Only for type "lookup" — which existing lookup feeds this dropdown.
    /// See Core.Constants.ServiceLookupSources.
    /// </summary>
    public string SourceKey { get; set; }

    // ── Optional constraints ────────────────────────────────────────────────
    // Deliberately a short, fixed list rather than an expression language: each
    // one is a single control in the builder and produces a sentence a
    // non-technical user can act on.

    /// <summary>number: smallest accepted value.</summary>
    public decimal? Min { get; set; }

    /// <summary>number: largest accepted value.</summary>
    public decimal? Max { get; set; }

    /// <summary>text / textarea: minimum characters.</summary>
    public int? MinLength { get; set; }

    /// <summary>text / textarea: maximum characters.</summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// date / datetime: key of an earlier date field this one must not precede
    /// — e.g. "arrives" after "departs", "checkOut" after "checkIn".
    /// </summary>
    public string AfterField { get; set; }

    /// <summary>date / datetime: must fall inside the guest's event dates.</summary>
    public bool WithinEventDates { get; set; }

    /// <summary>
    /// file: which file kinds the picker should offer, as an HTML accept string
    /// (".pdf,.jpg,.png"). A hint for the browser's dialog, not a guarantee —
    /// the blob layer decides what it will actually store.
    /// </summary>
    public string Accept { get; set; }
}

/// <summary>
/// Shows a section only when another field holds one of the listed values —
/// e.g. the Outbound leg appears for trip type "outbound" or "return".
/// Section level rather than field level on purpose: it covers the real cases
/// with one control an admin can reason about, instead of a rule per field.
/// </summary>
public class ServiceSectionCondition
{
    public string Field { get; set; }
    public List<string> Values { get; set; } = new();
}

public class ServiceFormSection
{
    public string Key { get; set; }
    public string Label { get; set; }
    public string LabelAr { get; set; }
    public ServiceSectionCondition VisibleWhen { get; set; }
    public List<ServiceFieldDefinition> Fields { get; set; } = new();
}

public class ServiceFormDefinition
{
    public List<ServiceFormSection> Sections { get; set; } = new();

    /// <summary>Every field across every section, in render order.</summary>
    [JsonIgnore]
    public IEnumerable<ServiceFieldDefinition> AllFields =>
        Sections.SelectMany(s => s.Fields ?? new List<ServiceFieldDefinition>());

    /// <summary>Sections whose condition is satisfied by the given values.</summary>
    public IEnumerable<ServiceFormSection> VisibleSections(Dictionary<string, string> values)
        => Sections.Where(s => IsSectionVisible(s, values));

    public static bool IsSectionVisible(ServiceFormSection section, Dictionary<string, string> values)
    {
        var c = section?.VisibleWhen;
        if (c == null || string.IsNullOrWhiteSpace(c.Field) || c.Values == null || c.Values.Count == 0)
            return true;

        values ??= new Dictionary<string, string>();
        return values.TryGetValue(c.Field, out var actual)
               && c.Values.Any(v => string.Equals(v, actual, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Reads and writes <c>Service.FormSchemaJson</c> and <c>GuestServiceEntry.ValuesJson</c>.
///
/// Sections are presentation only: field keys are unique across the whole form and
/// values stay a flat map, so reading one is a dictionary lookup and re-grouping the
/// form later never migrates stored data.
/// </summary>
public static class ServiceFormSchema
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Never throws: malformed or legacy JSON reads as an empty form.</summary>
    public static ServiceFormDefinition Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new ServiceFormDefinition();
        try
        {
            return JsonSerializer.Deserialize<ServiceFormDefinition>(json, Opts)
                   ?? new ServiceFormDefinition();
        }
        catch (JsonException)
        {
            return new ServiceFormDefinition();
        }
    }

    public static string Serialize(ServiceFormDefinition form)
        => JsonSerializer.Serialize(form ?? new ServiceFormDefinition(), Opts);

    public static Dictionary<string, string> ParseValues(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, Opts)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    public static string SerializeValues(Dictionary<string, string> values)
        => JsonSerializer.Serialize(values ?? new Dictionary<string, string>(), Opts);

    /// <summary>
    /// Structural check on an admin-authored form. Returns null when valid.
    /// </summary>
    public static string ValidateForm(ServiceFormDefinition form)
    {
        if (form == null || form.Sections.Count == 0)
            return "A service form needs at least one section.";

        var seenSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenField = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in form.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Key))
                return "Every section needs a key.";
            if (!seenSection.Add(section.Key))
                return $"Duplicate section key '{section.Key}'.";
            if (section.Fields == null || section.Fields.Count == 0)
                return $"Section '{section.Label ?? section.Key}' has no fields.";

            var cond = section.VisibleWhen;
            if (cond != null && !string.IsNullOrWhiteSpace(cond.Field))
            {
                var target = form.AllFields.FirstOrDefault(f =>
                    string.Equals(f.Key, cond.Field, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                    return $"Section '{section.Label ?? section.Key}' depends on a field '{cond.Field}' that does not exist.";
                if (cond.Values == null || cond.Values.Count == 0)
                    return $"Section '{section.Label ?? section.Key}' has a condition with no values selected.";
            }

            foreach (var f in section.Fields)
            {
                if (string.IsNullOrWhiteSpace(f.Key))
                    return "Every field needs a key.";
                // Unique across the whole form, not just the section — values are
                // stored flat, so two sections sharing a key would overwrite.
                if (!seenField.Add(f.Key))
                    return $"Duplicate field key '{f.Key}'. Field keys must be unique across the whole form.";
                if (string.IsNullOrWhiteSpace(f.Label))
                    return $"Field '{f.Key}' needs a label.";
                if (!ServiceFieldTypes.IsValid(f.Type))
                    return $"Field '{f.Key}' has an unknown type '{f.Type}'.";
                if (!string.IsNullOrWhiteSpace(f.AfterField)
                    && !form.AllFields.Any(x => string.Equals(x.Key, f.AfterField, StringComparison.OrdinalIgnoreCase)))
                    return $"Field '{f.Label}' is ordered against '{f.AfterField}', which does not exist.";
                if (f.Type == ServiceFieldTypes.Lookup && !ServiceLookupSources.IsValid(f.SourceKey))
                    return $"Field '{f.Label}' pulls from a lookup, so it needs a valid source.";
                if (f.Type == ServiceFieldTypes.Select && (f.Options == null || f.Options.Count == 0))
                    return $"Field '{f.Label}' is a dropdown, so it needs at least one option.";
            }
        }

        return null;
    }

    /// <summary>
    /// Checks submitted values against the form. Returns the labels of required
    /// fields left empty; an empty list means the entry may be marked completed.
    /// </summary>
    public static List<string> MissingRequired(ServiceFormDefinition form, Dictionary<string, string> values)
    {
        values ??= new Dictionary<string, string>();
        // Only visible sections count: a hidden leg's required fields must not
        // block completion, or an inbound-only booking could never be finished.
        return form.VisibleSections(values)
            .SelectMany(s => s.Fields ?? new List<ServiceFieldDefinition>())
            .Where(f => f.Required)
            .Where(f => !values.TryGetValue(f.Key, out var v) || string.IsNullOrWhiteSpace(v))
            .Select(f => f.Label ?? f.Key)
            .ToList();
    }

    /// <summary>
    /// Constraint violations on the values that WERE supplied — ranges, lengths,
    /// date ordering and the event window. Independent of
    /// <see cref="MissingRequired"/>: a blank optional field is fine, a filled
    /// one that breaks its rule is not, so both run on save.
    /// </summary>
    public static List<string> ConstraintErrors(
        ServiceFormDefinition form,
        Dictionary<string, string> values,
        DateOnly? eventStart = null,
        DateOnly? eventEnd = null)
    {
        values ??= new Dictionary<string, string>();
        var errors = new List<string>();

        var visible = form.VisibleSections(values)
            .SelectMany(s => s.Fields ?? new List<ServiceFieldDefinition>())
            .ToList();

        foreach (var f in visible)
        {
            if (!values.TryGetValue(f.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;

            var label = f.Label ?? f.Key;

            if (f.Type == ServiceFieldTypes.Number)
            {
                if (!decimal.TryParse(raw, out var n))
                {
                    errors.Add($"{label} must be a number.");
                    continue;
                }
                if (f.Min.HasValue && n < f.Min.Value) errors.Add($"{label} must be at least {f.Min}.");
                if (f.Max.HasValue && n > f.Max.Value) errors.Add($"{label} must be at most {f.Max}.");
            }

            if (f.Type is ServiceFieldTypes.Text or ServiceFieldTypes.Textarea)
            {
                if (f.MinLength.HasValue && raw.Trim().Length < f.MinLength.Value)
                    errors.Add($"{label} must be at least {f.MinLength} characters.");
                if (f.MaxLength.HasValue && raw.Trim().Length > f.MaxLength.Value)
                    errors.Add($"{label} must be at most {f.MaxLength} characters.");
            }

            // A file field holds the URL the upload returned, never a filename.
            // Catching that here is what stops a stale client persisting
            // "visa.pdf", which would render as a broken link forever after.
            if (f.Type == ServiceFieldTypes.File && !IsWebUrl(raw))
            {
                errors.Add($"{label} must be an uploaded file.");
                continue;
            }

            if (f.Type is ServiceFieldTypes.Date or ServiceFieldTypes.DateTime)
            {
                if (!TryParseWhen(raw, out var when))
                {
                    errors.Add($"{label} is not a valid date.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(f.AfterField)
                    && values.TryGetValue(f.AfterField, out var otherRaw)
                    && !string.IsNullOrWhiteSpace(otherRaw)
                    && TryParseWhen(otherRaw, out var other)
                    && when < other)
                {
                    var otherLabel = visible.FirstOrDefault(x =>
                        string.Equals(x.Key, f.AfterField, StringComparison.OrdinalIgnoreCase))?.Label
                        ?? f.AfterField;
                    errors.Add($"{label} must be after {otherLabel}.");
                }

                if (f.WithinEventDates && (eventStart.HasValue || eventEnd.HasValue))
                {
                    var day = DateOnly.FromDateTime(when);
                    if (eventStart.HasValue && day < eventStart.Value)
                        errors.Add($"{label} is before the event starts ({eventStart:yyyy-MM-dd}).");
                    if (eventEnd.HasValue && day > eventEnd.Value)
                        errors.Add($"{label} is after the event ends ({eventEnd:yyyy-MM-dd}).");
                }
            }
        }

        return errors;
    }

    private static bool IsWebUrl(string raw) =>
        Uri.TryCreate(raw?.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool TryParseWhen(string raw, out DateTime when) =>
        DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out when);

    /// <summary>
    /// Drops keys the form does not define, so a stale client cannot persist
    /// fields that were removed from the schema.
    /// </summary>
    public static Dictionary<string, string> StripUnknown(
        ServiceFormDefinition form, Dictionary<string, string> values)
    {
        var known = form.AllFields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return (values ?? new Dictionary<string, string>())
            .Where(kv => known.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}

/// <summary>Status of a <c>GuestServiceEntry</c>. Absence of a row means pending.</summary>
public static class GuestServiceStatus
{
    public const string Pending = "pending";
    public const string Completed = "completed";

    public static bool IsValid(string s) => s == Pending || s == Completed;
}
