namespace BusinessLayer.DTOs.Validation
{
    public class ImageValidationResponse
    {
        public bool IsInappropriate { get; set; }
        public string? Reason { get; set; }
    }
}
