using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IOtpService
    {
        Task SendOtpAsync(string email, OtpPurpose purpose);                        // tạo + lưu Redis (TTL) + gửi mail
        Task<bool> VerifyOtpAsync(string email, string code, OtpPurpose purpose);   // kiểm tra mã, set cờ verified (TTL)
        Task<bool> IsVerifiedAsync(string email, OtpPurpose purpose);
        Task ConsumeVerifiedFlagAsync(string email, OtpPurpose purpose);            // xóa cờ verified sau khi register
    }

}
