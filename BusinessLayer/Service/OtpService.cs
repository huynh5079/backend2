using BusinessLayer.Options;
using BusinessLayer.Service.Interface;
using DataLayer.Enum;
using DataLayer.Repositories.Abstraction;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class OtpService : IOtpService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IEmailService _email;
        private readonly IUserRepository _users;
        private readonly TimeSpan _otpTtl = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _flagTtl = TimeSpan.FromMinutes(15);

        public OtpService(IConnectionMultiplexer redis, IEmailService email, IUserRepository users)
        {
            _redis = redis;
            _email = email;
            _users = users;
        }

        // Tạo prefix theo purpose
        private static string Pfx(OtpPurpose p) => p switch
        {
            OtpPurpose.Register => "reg",
            OtpPurpose.ResetPassword => "fp",
            _ => "otp"
        };

        private static string KeyOtp(string pfx, string email) => $"{pfx}:otp:{email}";
        private static string KeyFlag(string pfx, string email) => $"{pfx}:verified:{email}";
        private static string KeyThrottle(string pfx, string email) => $"{pfx}:limit:{email}";

        public async Task SendOtpAsync(string email, OtpPurpose purpose)
        {
            email = email.Trim().ToLowerInvariant();
            var pfx = Pfx(purpose);

            // Rule khác nhau tùy purpose:
            if (purpose == OtpPurpose.Register)
            {
                if (await _users.ExistsByEmailAsync(email))
                    throw new InvalidOperationException("email đã tồn tại");
            }
            else if (purpose == OtpPurpose.ResetPassword)
            {
                if (!await _users.ExistsByEmailAsync(email))
                    throw new InvalidOperationException("email không tồn tại");
            }

            var db = _redis.GetDatabase();
            if (await db.StringGetAsync(KeyThrottle(pfx, email)) != RedisValue.Null)
                throw new InvalidOperationException("Vui lòng thử lại sau vài giây.");

            var code = Random.Shared.Next(100000, 999999).ToString();
            await db.StringSetAsync(KeyOtp(pfx, email), code, _otpTtl);
            await db.StringSetAsync(KeyThrottle(pfx, email), "1", TimeSpan.FromSeconds(30));

            await _email.SendOtpEmailAsync(email, code);
        }

        public async Task<bool> VerifyOtpAsync(string email, string code, OtpPurpose purpose)
        {
            email = email.Trim().ToLowerInvariant();
            var pfx = Pfx(purpose);

            var db = _redis.GetDatabase();
            var cached = await db.StringGetAsync(KeyOtp(pfx, email));
            if (cached.IsNullOrEmpty) return false;

            var ok = string.Equals(cached.ToString(), code, StringComparison.Ordinal);
            if (!ok) return false;

            await db.KeyDeleteAsync(KeyOtp(pfx, email));
            await db.StringSetAsync(KeyFlag(pfx, email), "1", _flagTtl);
            return true;
        }

        public async Task<bool> IsVerifiedAsync(string email, OtpPurpose purpose)
        {
            var db = _redis.GetDatabase();
            return await db.StringGetAsync(KeyFlag(Pfx(purpose), email.Trim().ToLowerInvariant())) != RedisValue.Null;
        }

        public async Task ConsumeVerifiedFlagAsync(string email, OtpPurpose purpose)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(KeyFlag(Pfx(purpose), email.Trim().ToLowerInvariant()));
        }
    }
}
