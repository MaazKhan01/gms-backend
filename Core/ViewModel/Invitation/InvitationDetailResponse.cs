using System;

namespace Core.ViewModel.Invitation
{
    // Shape returned to the PUBLIC (no-login) invitation page. Deliberately
    // minimal — only what the guest needs to see to decide; no internal ids,
    // tokens, or other guests' data.
    public class InvitationDetailResponse
    {
        public string GuestName { get; set; }
        public string Tier { get; set; }
        public string EventTitle { get; set; }
        public string EventVenue { get; set; }
        public DateOnly? EventStartDate { get; set; }
        public DateOnly? EventEndDate { get; set; }
        public string InvitationStatus { get; set; }
        // True once the guest has accepted or declined — the page then shows a
        // confirmation state instead of the Accept/Reject buttons.
        public bool AlreadyResponded { get; set; }
    }
}
