using DataLayer.Enum;
using System.ComponentModel.DataAnnotations;

namespace BusinessLayer.DTOs.Schedule.ClassRequest
{
    public class UpdateStatusDto
    {
        [StringLength(200, ErrorMessage = "Hình thức học (Online/Ofline) là bắt buộc")]
        public ClassRequestStatus Status { get; set; }
    }
}