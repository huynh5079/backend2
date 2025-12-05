using DataLayer.Enum;
using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Schedule.ClassRequest
{
    public class UpdateStatusDto
    {
        public ClassRequestStatus Status { get; set; }

        // Add for link tranfer
        public string? MeetingLink { get; set; }
    }
}