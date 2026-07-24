using System;

namespace Core.Common.Interfaces;

// Resolves the calling guest from the guest JWT. Separate from ICurrentUser,
// which resolves against the Users table — a guest id is not a user id.
public interface ICurrentGuest
{
    int GuestId { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
}
