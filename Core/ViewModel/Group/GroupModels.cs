using System;

namespace Core.ViewModel.Group;

/// <summary>A delegation sub-group — a name and nothing else, by design. What a
/// group MEANS is decided by who is in it on a given mission, not by fields
/// here.</summary>
public class CreateGroupRequest
{
    public string Name { get; set; }
}

public class UpdateGroupRequest : CreateGroupRequest { }

public class GroupResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    /// <summary>Delegates currently in this group, across every mission. Shown in
    /// the lookup table so an admin can see which groups are actually in use
    /// before renaming one.</summary>
    public int MemberCount { get; set; }
}
