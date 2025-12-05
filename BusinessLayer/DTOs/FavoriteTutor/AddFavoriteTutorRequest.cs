using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.FavoriteTutor
{
    public record AddFavoriteTutorRequest
    {
        [Required(ErrorMessage = "TutorProfileId là bắt buộc")]
        public string TutorProfileId { get; init; } = "";
    }
}
