using System;

namespace Core.ViewModel.Guest;

/// <summary>
/// Bare minimum for a guest picker row (name, avatar, subtitle). Deliberately
/// separate from <see cref="GuestResponse"/>: that one drags in nationality,
/// event, sessions and invitation state, which a dropdown never shows and which
/// cost several joins plus an extra invitation query per page.
/// </summary>
public class GuestPickerResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Organization { get; set; }
    public string Tier { get; set; }
    public string PhotoUrl { get; set; }
}
