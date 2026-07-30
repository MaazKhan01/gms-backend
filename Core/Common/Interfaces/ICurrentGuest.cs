using System;

namespace Core.Common.Interfaces;

// Resolves the calling guest's Guest.Id from the guest JWT, which carries only
// the linked User.Id (see CurrentGuest). Separate from ICurrentUser: same
// person, different key — Guest.Id, not Users.Id.
public interface ICurrentGuest
{
    int GuestId { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
}
