namespace BusinessLayer.DTOs.Quiz
{
    /// <summary>
    /// AI response for image content validation
    /// </summary>
    public class ImageValidationResponse
    {
        public bool IsInappropriate { get; set; }
        public string? Reason { get; set; }
    }
}
