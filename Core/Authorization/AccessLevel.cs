namespace Core.Authorization;

/// <summary>The two independent access levels a role can hold on a permission.</summary>
public enum AccessLevel
{
    /// <summary>View the page and read its data.</summary>
    Read = 0,

    /// <summary>Create / update / delete through it.</summary>
    Write = 1,
}
