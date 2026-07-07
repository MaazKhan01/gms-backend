namespace Core.ViewModel.Lookup;

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
