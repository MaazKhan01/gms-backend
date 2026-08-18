using System;

namespace Core.ViewModel.Guest;

/// <summary>
/// Bare minimum for a guest picker row (name, avatar, subtitle). Deliberately
/// separate from <see cref="GuestResponse"/>: that one drags in nationality,
/// event, sessions and invitation state, which a dropdown never shows and which
/// cost several joins plus an extra invitation query per page.
/// </summary>
/// <remarks><b>Frontend contract:</b> <see cref="Id"/> is an
/// <c>EventGuest.PublicId</c> — pickers feed seating / meetings / travel, all of
/// which are event-scoped.</remarks>
public class GuestPickerResponse
{
    /// <summary>EventGuest.PublicId — this person's participation in the picked event.</summary>
    public Guid Id { get; set; }
    /// <summary>Guest.PublicId — the person behind it.</summary>
    public Guid PersonId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Organization { get; set; }
    public string Tier { get; set; }
    public string PhotoUrl { get; set; }
}
