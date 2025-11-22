using DataLayer.Enum;
using System;

namespace BusinessLayer.DTOs.Schedule.ClassAssign
{
    public class MyEnrolledClassesDto
    {
        public string ClassId { get; set; } = null!;
        public string ClassTitle { get; set; } = null!;
        public string? Subject { get; set; }
        public string? EducationLevel { get; set; }
        public string? TutorName { get; set; }
        public decimal Price { get; set; }
        public ClassStatus ClassStatus { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public DateTime? EnrolledAt { get; set; }
        public string? Location { get; set; }
        public ClassMode Mode { get; set; }
        public DateTime? ClassStartDate { get; set; }
    }
}

