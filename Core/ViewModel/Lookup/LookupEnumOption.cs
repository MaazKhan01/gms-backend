namespace Core.ViewModel.Lookup;

/// <summary>A code-defined option keyed by an int enum value (DriverType, …).
/// The client posts <see cref="Value"/> back verbatim.</summary>
public class EnumIntOption(int value, string name, string nameAr)
{
    public int Value { get; set; } = value;
    public string Name { get; set; } = name;
    public string NameAr { get; set; } = nameAr;
}

/// <summary>A single code-defined option (not stored in the DB).</summary>
public class LookupEnumOption
{
    public string Code { get; set; }
    public string Name { get; set; }
    public string NameAr { get; set; }

    public LookupEnumOption() { }

    public LookupEnumOption(string code, string name, string nameAr)
    {
        Code = code;
        Name = name;
        NameAr = nameAr;
    }
}
