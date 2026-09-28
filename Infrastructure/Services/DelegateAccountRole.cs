using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Constants;
using Core.Interfaces.Repositories;

namespace Infrastructure.Services;

/// <summary>
/// Keeps a delegate's LOGIN in step with what they do on a mission.
///
/// Two different things wear the word "role" here, and they are not the same:
///
///  * the MISSION role (EventGuest.MissionRoleId) — Head of Delegation, Member,
///    Support Staff. What the person does on the ground.
///  * the ACCOUNT role (User.RoleId) — what their login can open.
///
/// For most delegates the account role is `guest` (labelled "Delegate"), which
/// has PortalAccess = false: they authenticate into the VIP app by OTP and never
/// see the portal. But some mission roles are ALSO portal roles — Head of
/// Delegation travels with the delegation and signs in to review the mission —
/// and those people were still being given the no-access account, so they could
/// not get in at all and their role read "Delegate" everywhere.
///
/// The rule: if any of this person's participations carries a mission role that
/// grants portal access, their account takes that role. Otherwise it falls back
/// to the plain delegate account. Recomputed from all participations rather than
/// set once, so losing the Head of Delegation role on one mission takes the
/// access away again — unless another mission still grants it.
/// </summary>
internal static class DelegateAccountRole
{
    public static async Task SyncAsync(IUnitOfWork unitOfWork, int guestId, CancellationToken ct)
    {
        var guest = await unitOfWork.Guests.Query()
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == guestId, ct);

        if (guest?.User == null) return;

        // The highest-privilege mission role the person holds anywhere. Ordered
        // so the answer is stable when someone is Head of Delegation on one
        // mission and a Member on another.
        var portalRoleId = await unitOfWork.EventGuests.QueryNoTracking()
            .Where(eg => eg.GuestId == guestId
                      && eg.MissionRole != null
                      && eg.MissionRole.PortalAccess)
            .OrderBy(eg => eg.MissionRole.Name)
            .Select(eg => (int?)eg.MissionRoleId)
            .FirstOrDefaultAsync(ct);

        int? targetRoleId;
        if (portalRoleId.HasValue)
        {
            targetRoleId = portalRoleId;
        }
        else
        {
            var delegateRole = await unitOfWork.Roles.QueryNoTracking()
                .FirstOrDefaultAsync(r => r.Code == Roles.GUEST, ct);
            targetRoleId = delegateRole?.Id;
        }

        // Never touch an account that was deliberately given a staff role — an
        // administrator who also travels keeps their administrator login.
        var current = await unitOfWork.Roles.QueryNoTracking()
            .FirstOrDefaultAsync(r => r.Id == guest.User.RoleId, ct);

        var currentIsDelegateAccount = current == null
            || current.Code == Roles.GUEST
            || current.IsDelegateRole;

        if (!currentIsDelegateAccount) return;
        if (guest.User.RoleId == targetRoleId) return;

        guest.User.RoleId = targetRoleId;
        unitOfWork.Users.Update(guest.User);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
