namespace Core.ViewModel.Location
{
    public class CreateLocationDto
    {
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string? Address { get; set; }
        public string? Type { get; set; }
    }
}
