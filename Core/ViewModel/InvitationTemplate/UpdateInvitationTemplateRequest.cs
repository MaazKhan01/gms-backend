using System.Collections.Generic;

namespace Core.ViewModel.InvitationTemplate;

public class UpdateInvitationTemplateRequest
{
    public string? Name { get; set; }
    public string? NameAr { get; set; }
    public string? Language { get; set; }
    public string? Subject { get; set; }
    public string? SubjectAr { get; set; }
    public string? Body { get; set; }
    public string? BodyAr { get; set; }
    public List<string>? TargetTiers { get; set; }
    public string? Color { get; set; }
    public bool? IsActive { get; set; }
}
