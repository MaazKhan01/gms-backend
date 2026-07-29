namespace Core.Helpers;

/// <summary>
/// Users.Phone is one column holding "+971 501234567" — dial code, a space, then
/// the national number. Clients that want the two halves separately (the driver
/// app) get them split here instead of the DB carrying two columns.
/// </summary>
public static class PhoneParts
{
    /// <summary>Splits a stored phone into (dialCode, nationalNumber). A value with
    /// no space is treated as all-number, dial code unknown.</summary>
    public static (string Code, string Number) Split(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return (null, null);

        var trimmed = phone.Trim();
        var space = trimmed.IndexOf(' ');
        if (space <= 0) return (null, trimmed);

        // ponytail: split on the first space, no dial-code table. Numbers are
        // written by the portal's phone picker, which always emits that shape.
        return (trimmed[..space], trimmed[(space + 1)..].TrimStart());
    }

    /// <summary>Rejoins into the stored form. Either half may be missing.</summary>
    public static string Join(string code, string number)
    {
        code = code?.Trim();
        number = number?.Trim();

        if (string.IsNullOrEmpty(number)) return string.IsNullOrEmpty(code) ? null : code;
        if (string.IsNullOrEmpty(code)) return number;

        if (!code.StartsWith('+')) code = "+" + code;
        return $"{code} {number}";
    }
}
