using BusinessLayer.Options;
using BusinessLayer.Service.Interface;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _cfg;
        private readonly IWebHostEnvironment _env;

        public EmailService(IOptions<EmailSettings> cfg, IWebHostEnvironment env)
        {
            _cfg = cfg.Value;
            _env = env;
        }

        public async Task SendOtpEmailAsync(string toEmail, string otpCode)
        {
            string html = LoadTemplate("otp.html");
            html = ReplaceTokens(html, new Dictionary<string, string>
            {
                ["BRAND_NAME"] = _cfg.SenderName ?? "TPEdu Center",
                ["BRAND_URL"] = "#",
                ["LOGO_URL"] = "https://res.cloudinary.com/dwmfmq5xa/image/upload/v1758521309/plugins_email-verification-plugin_m7tyci.png", // https://dummyimage.com/120x36/4f46e5/ffffff&text=MyApp
                ["SUPPORT_EMAIL"] = _cfg.SenderEmail,
                ["OTP_CODE"] = otpCode,
                ["EXPIRE_MINUTES"] = "5",
                ["TO_NAME"] = toEmail,
                ["YEAR"] = DateTime.Now.Year.ToString()
            });

            await SendAsync(toEmail, "Mã xác minh tài khoản (OTP)", html);
        }

        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_cfg.SenderName, _cfg.SenderEmail));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = subject;

            var body = new BodyBuilder { HtmlBody = htmlBody, TextBody = StripHtml(htmlBody) };
            msg.Body = body.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_cfg.SmtpServer, _cfg.Port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_cfg.SenderEmail, _cfg.Password);
            await smtp.SendAsync(msg);
            await smtp.DisconnectAsync(true);
        }

        // Helpers
        private string LoadTemplate(string fileName)
        {
            var path = Path.Combine(_env.ContentRootPath, "EmailTemplates", fileName);
            if (!File.Exists(path)) throw new FileNotFoundException("Không tìm thấy template: " + path);
            return File.ReadAllText(path);
        }

        private static string ReplaceTokens(string html, IDictionary<string, string> tokens)
        {
            foreach (var kv in tokens)
            {
                var val = WebUtility.HtmlEncode(kv.Value);
                html = html.Replace("{{" + kv.Key + "}}", val);
            }
            return html;
        }

        private static string StripHtml(string html)
            => System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
    }
}
