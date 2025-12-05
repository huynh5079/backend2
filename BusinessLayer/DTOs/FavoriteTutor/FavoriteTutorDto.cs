using System;

namespace BusinessLayer.DTOs.FavoriteTutor
{
    public record FavoriteTutorDto
    {
        public string Id { get; init; } = "";
        public string TutorProfileId { get; init; } = "";
        public string TutorUserId { get; init; } = "";
        public string TutorName { get; init; } = "";
        public string? TutorAvatar { get; init; }
        public string? TutorBio { get; init; }
        public double? TutorRating { get; init; }
        public DateTime FavoritedAt { get; init; }
    }
}
