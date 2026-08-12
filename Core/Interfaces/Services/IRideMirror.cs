using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Interfaces.Services;

/// <summary>
/// Mirrors a ride (Transports row) into Firestore so the driver app and the VIP
/// app can *listen* for changes instead of polling the API — a guest's request
/// shows up in the drivers' pool, and the accept/lifecycle updates land back on
/// the guest's screen, both without a refresh.
///
/// SQL Server stays the source of truth. This is a one-way projection: nothing
/// reads back from Firestore, and a failed mirror is logged and swallowed — the
/// API call it hangs off still succeeds. Call it after every write that changes
/// a ride (create, accept, status advance, cancel).
/// </summary>
public interface IRideMirror
{
    Task SyncAsync(Guid transportPublicId, CancellationToken ct = default);
}
