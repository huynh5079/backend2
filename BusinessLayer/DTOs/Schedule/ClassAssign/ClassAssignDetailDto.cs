using DataLayer.Enum;
using System;

namespace BusinessLayer.DTOs.Schedule.ClassAssign
{
    public class ClassAssignDetailDto
    {
        public string ClassAssignId { get; set; } = null!;
        public string ClassId { get; set; } = null!;
        public string ClassTitle { get; set; } = null!;
        public string? ClassDescription { get; set; }
        public string? ClassSubject { get; set; }
        public string? ClassEducationLevel { get; set; }
        public decimal ClassPrice { get; set; }
        public ClassStatus ClassStatus { get; set; }
        public string StudentId { get; set; } = null!;
        public string StudentName { get; set; } = null!;
        public string? StudentEmail { get; set; }
        public string? StudentPhone { get; set; }
        public string? StudentAvatarUrl { get; set; }
        public ApprovalStatus ApprovalStatus { get; set; }
        public PaymentStatus PaymentStatus { get; set; }
        public DateTime? EnrolledAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

