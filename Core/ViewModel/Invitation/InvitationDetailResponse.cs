using System;
using System.Collections.Generic;

namespace Core.ViewModel.Invitation
{
    // Shape returned to the PUBLIC (no-login) invitation page. Enough for the
    // guest to recognise themselves and know what to expect — no internal ids,
    // tokens, or other guests' data, and no per-service field DETAIL (that's
    // configured after acceptance, in the VIP app / on-site, not here).
    public class InvitationDetailResponse
    {
        public string GuestName { get; set; }
        public string GuestEmail { get; set; }
        public string GuestPhotoUrl { get; set; }
        public string Organization { get; set; }
 
        public string ServiceLevelName { get; set; }
        public string ServiceLevelNameAr { get; set; }
        public string ServiceLevelColor { get; set; }

        public string EventTitle { get; set; }
        public string EventVenue { get; set; }
        public DateOnly? EventStartDate { get; set; }
        public DateOnly? EventEndDate { get; set; }

        // Names only — "you'll be facilitated with the following services" —
        // never field values or completion status.
        public List<InvitationServiceItem> Services { get; set; } = new();

        public string InvitationStatus { get; set; }
        // True once the guest has accepted or declined — the page then shows a
        // confirmation state instead of the Accept/Reject buttons.
        public bool AlreadyResponded { get; set; }
    }

    public class InvitationServiceItem
    {
        public string Name { get; set; }
        public string NameAr { get; set; }
        public string Icon { get; set; }
    }
}
