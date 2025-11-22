using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Authentication.Register
{
    public class RegisterStudentRequest
    {
        [Required(ErrorMessage = "Yêu cầu nhập địa chỉ email và phải đúng cấu trúc"), EmailAddress]
        public string Email { get; set; } = default!;

        [Required(ErrorMessage = "Nhập tên của bạn")]
        public string Username { get; set; } = default!;

        [Required(ErrorMessage = "Yêu cầu nhập mật khẩu phải tối thiểu phải từ 8 kí tự trở lên"), MinLength(8)]
        public string Password { get; set; } = default!;

        [Required(ErrorMessage = "Yêu cầu nhập ngày tháng năm sinh")]
        public DateOnly DateOfBirth { get; set; }
    }
}
