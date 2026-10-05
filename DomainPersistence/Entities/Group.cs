using System.Collections.Generic;

namespace DomainPersistence.Entities;

/// <summary>
/// A named sub-group of a delegation — "Advance Party", "Media Team", "Security
/// Detail". Admin-managed reference data, a name and nothing else.
///
/// Global rather than per-mission, like every other lookup here: the same few
/// names recur across missions, and scoping them per mission would mean
/// re-creating "Advance Party" for each one. What is per-mission is the
/// MEMBERSHIP — <see cref="EventGuest.GroupId"/> — since a delegate belongs to a
/// group only within the mission they are nominated to.
///
/// One group serves two screens that used to disagree: the nomination sub-group
/// (previously free text, so "Team A" and "Team A " were different groups) and
/// the transport group booking, which books one ride for everyone in it.
/// </summary>
public class Group : Entity
{
    public string Name { get; set; }

    public virtual ICollection<EventGuest> EventGuests { get; set; } = new List<EventGuest>();
}
