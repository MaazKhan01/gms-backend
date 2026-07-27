namespace Core.ViewModel.Noification;

// Response for POST /api/v1/notifications/send. Deliberately just a count +
// which audience/strategy was used — the admin caller wants confirmation, not
// the full created-row payload (which differs in shape between Users and Guests).
public class SendNotificationResult
{
    public int RecipientCount { get; set; }
    public string TargetType { get; set; }
}
