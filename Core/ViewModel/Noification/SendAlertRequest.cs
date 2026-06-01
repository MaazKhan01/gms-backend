namespace Core.ViewModel.Noification;

public class NotificationRequestDto
{
    public Guid? UserId { get; set; }
    public string NotificationTemplateCode { get; set; }
    public IDictionary<string, string> Tags { get; set; }
}

public class SendAlertRequest : NotificationRequestDto
{
    public string Title { get; set; }
    public string Message { get; set; }
    public string Url { get; set; }
    public string Topic { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
}
