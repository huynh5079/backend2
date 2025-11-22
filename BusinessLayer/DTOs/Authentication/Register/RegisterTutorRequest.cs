using DataLayer.Enum;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Authentication.Register
{
    public class RegisterTutorRequest
    {
        // Thông tin tài khoản
        [Required(ErrorMessage = "Nhập tên của bạn")]
        public string Username { get; set; } = default!;

        [Required(ErrorMessage = "Yêu cầu nhập mật khẩu phải tối thiểu phải từ 8 kí tự trở lên"), MinLength(8)]
        public string Password { get; set; } = default!;

        [Required(ErrorMessage = "Yêu cầu nhập địa chỉ email và phải đúng cấu trúc"), EmailAddress]
        public string Email { get; set; } = default!;

        // Thông tin cá nhân
        [Required]
        public Gender? Gender { get; set; }

        [Required]
        public DateOnly? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập số điện thoại và phải đúng cấu trúc"), Phone]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Yêu cầu địa chỉ phải được nhập")]
        public string? Address { get; set; }

        public string? SelfDescription { get; set; }

        // Thông tin học vấn
        [Required(ErrorMessage = "Yêu cầu nhập trình độ học vấn")]
        public string? EducationLevel { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập tên trường đang theo học")]
        public string? University { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập chuyên ngành theo học")]
        public string? Major { get; set; }

        // Thông tin giảng dạy
        [Required(ErrorMessage = "Yêu cầu nhập số năm kinh nghiệm đã giảng dạy")]
        public int? TeachingExperienceYears { get; set; }

        public string? ExperienceDetails { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập môn giảng dạy")]
        public IEnumerable<string>? TeachingSubjects { get; set; }

        [Required(ErrorMessage = "Yêu cầu nhập trình độ bạn sẽ giảng day(Tiểu học,...)")]
        public IEnumerable<string>? TeachingLevel { get; set; }

        public IEnumerable<string>? SpecialSkills { get; set; }

        [Required(ErrorMessage = "Yêu cầu tải chứng chỉ lên")]
        public List<IFormFile>? CertificateFiles { get; set; }     // chứng chỉ

        [Required(ErrorMessage = "Yêu cầu tải căn cước công dân lên")]
        public List<IFormFile>? IdentityDocuments { get; set; }    // CCCD/CMND
    }
}
