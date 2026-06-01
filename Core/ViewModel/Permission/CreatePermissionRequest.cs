namespace Core.ViewModel.Permission;

public class CreatePermissionRequest
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string Module { get; set; }
    public string Description { get; set; }
}
