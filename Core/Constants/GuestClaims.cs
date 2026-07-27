namespace Core.Constants;

// Shared JWT claim name for a Guest's own internal id. Used when issuing the
// guest token (VipAppService), reading it back (CurrentGuest), and joining the
// per-guest SignalR group (RealTimeHubService, in Core — can't reference
// Infrastructure.Services.CurrentGuest directly, hence this Core-level constant).
//
// IMPORTANT: this claim must only ever be added to Guest tokens. A User token
// carrying it would let CurrentGuest resolve that User as a Guest using their
// own internal id — Guest and User ids are independent sequences that can
// collide (see ICurrentGuest's remarks).
public static class GuestClaims
{
    public const string GuestId = "Id";
}
