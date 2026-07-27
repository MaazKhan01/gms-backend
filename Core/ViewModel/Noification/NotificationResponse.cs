namespace Core.ViewModel.Noification;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public string Type { get; set; }
    public bool? Read { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string RedirectUrl { get; set; }
    public string Data { get; set; }
}
