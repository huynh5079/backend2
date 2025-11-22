using DataLayer.Enum;
using DataLayer.Entities;
using System;
using System.Collections.Generic;

namespace DataLayer.Entities; 

public partial class TutorProfile : BaseEntity 
{ 
    public string? UserId { get; set; } 

    // Thông tin cá nhân & giới thiệu
    public string? Bio { get; set; } // Dùng cho SelfDescription
    public double? Rating { get; set; } 
    public bool? ApprovedByAdmin { get; set; } 

    // Thông tin học vấn
    public string? EducationLevel { get; set; } 
    public string? University { get; set; } 
    public string? Major { get; set; } 
    
    // Thông tin giảng dạy và kinh nghiệm
    public int? TeachingExperienceYears { get; set; } 
    public string? ExperienceDetails { get; set; } // Chi tiết kinh nghiệm làm việc
    
    // Lưu dưới dạng chuỗi được phân tách (ví dụ: "Math,Physics")
    public string? TeachingSubjects { get; set; } 
    public string? TeachingLevel { get; set; } 
    public string? SpecialSkills { get; set; }

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
    public string? RejectReason { get; set; }         // lý do từ chối
    public string? ProvideNote { get; set; }          // yêu cầu bổ sung (note gửi gia sư)

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual ICollection<AvailabilityBlock> AvailabilityBlocks { get; set; } = new List<AvailabilityBlock>();
    public virtual ICollection<ClassRequest> ClassRequests { get; set; } = new List<ClassRequest>();
    public virtual ICollection<Class> Classes { get; set; } = new List<Class>();
    public virtual ICollection<Media> Certificates { get; set; } = new List<Media>();
    public virtual ICollection<ScheduleEntry> ScheduleEntries { get; set; } = new List<ScheduleEntry>();
    public virtual ICollection<TutorApplication> TutorApplications { get; set; } = new List<TutorApplication>();

}