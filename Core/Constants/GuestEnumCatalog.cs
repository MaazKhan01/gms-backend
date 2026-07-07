using System.Collections.Generic;
using Core.ViewModel.Lookup;

namespace Core.Constants;

/// <summary>
/// Code-defined guest option sets (tier, type, statuses) exposed to the frontend
/// so it never hardcodes these lists. The value strings are the lookup "set codes"
/// the client requests; each set is a stable, DB-free enum with EN/AR labels.
/// </summary>
public static class GuestEnumCatalog
{
    public const string Tier                = "GuestTier";
    public const string Type                = "GuestType";
    public const string InvitationStatus    = "GuestInvitationStatus";
    public const string AccreditationStatus = "GuestAccreditationStatus";

    public static IReadOnlyDictionary<string, List<LookupEnumOption>> All { get; } =
        new Dictionary<string, List<LookupEnumOption>>
        {
            [Tier] = new()
            {
                new(GuestTier.VVIP,     "VVIP",     "شخصية مهمة جدًا"),
                new(GuestTier.VIP,      "VIP",      "شخصية مهمة"),
                new(GuestTier.Speaker,  "Speaker",  "متحدث"),
                new(GuestTier.Delegate, "Delegate", "مندوب"),
                new(GuestTier.Press,    "Press",    "صحافة"),
                new(GuestTier.Observer, "Observer", "مراقب"),
            },
            [Type] = new()
            {
                new(GuestTypes.Dignitary, "Dignitary", "شخصية رفيعة"),
                new(GuestTypes.Delegate,  "Delegate",  "مندوب"),
                new(GuestTypes.Media,     "Media",     "إعلام"),
                new(GuestTypes.Staff,     "Staff",     "طاقم"),
                new(GuestTypes.VIP,       "VIP",       "شخصية مهمة"),
                new(GuestTypes.Observer,  "Observer",  "مراقب"),
            },
            [InvitationStatus] = new()
            {
                new(GuestInvitationStatus.NotSent,  "Not Sent", "لم يُرسل"),
                new(GuestInvitationStatus.Sent,     "Sent",     "مُرسل"),
                new(GuestInvitationStatus.Opened,   "Opened",   "مفتوح"),
                new(GuestInvitationStatus.Accepted, "Accepted", "مقبول"),
                new(GuestInvitationStatus.Declined, "Declined", "مرفوض"),
            },
            [AccreditationStatus] = new()
            {
                new(GuestAccreditationStatus.NotIssued, "Not Issued", "غير صادر"),
                new(GuestAccreditationStatus.Issued,    "Issued",     "صادر"),
                new(GuestAccreditationStatus.Revoked,   "Revoked",    "ملغى"),
            },
        };
}
