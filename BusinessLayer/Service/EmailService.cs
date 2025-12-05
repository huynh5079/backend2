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

        public async Task SendInvoiceEmailAsync(
            string toEmail,
            string customerName,
            string invoiceNumber,
            string orderId,
            string? transactionId,
            decimal amount,
            string description,
            string? classTitle = null,
            string? classSubject = null)
        {
            string html = LoadTemplate("invoice.html");
            
            // Format amount as VND
            var formattedAmount = amount.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VND";
            
            // Format payment date
            var paymentDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN"));
            
            // Handle conditional class info block
            var classInfoBlock = "";
            if (!string.IsNullOrWhiteSpace(classTitle) || !string.IsNullOrWhiteSpace(classSubject))
            {
                classInfoBlock = $@"
                                <p style=""margin:8px 0 0;color:#374151;font-size:14px;""><strong>Lớp học:</strong> {WebUtility.HtmlEncode(classTitle ?? "N/A")}</p>
                                <p style=""margin:4px 0 0;color:#374151;font-size:14px;""><strong>Môn học:</strong> {WebUtility.HtmlEncode(classSubject ?? "N/A")}</p>";
            }
            
            // Handle conditional transaction ID row
            var transactionIdRow = "";
            if (!string.IsNullOrWhiteSpace(transactionId))
            {
                transactionIdRow = $@"
                                <tr>
                                    <td style=""padding:12px 0;border-bottom:1px solid #e5e7eb;"">
                                        <span style=""color:#6b7280;font-size:14px;"">Mã giao dịch:</span>
                                    </td>
                                    <td align=""right"" style=""padding:12px 0;border-bottom:1px solid #e5e7eb;"">
                                        <span style=""color:#111827;font-size:14px;font-weight:500;"">{WebUtility.HtmlEncode(transactionId)}</span>
                                    </td>
                                </tr>";
            }
            
            // Replace tokens
            html = ReplaceTokens(html, new Dictionary<string, string>
            {
                ["BRAND_NAME"] = _cfg.SenderName ?? "TPEdu Center",
                ["BRAND_URL"] = "#",
                ["LOGO_URL"] = "https://res.cloudinary.com/dwmfmq5xa/image/upload/v1758521309/plugins_email-verification-plugin_m7tyci.png",
                ["SUPPORT_EMAIL"] = _cfg.SenderEmail,
                ["INVOICE_NUMBER"] = invoiceNumber,
                ["PAYMENT_DATE"] = paymentDate,
                ["CUSTOMER_NAME"] = WebUtility.HtmlEncode(customerName),
                ["CUSTOMER_EMAIL"] = WebUtility.HtmlEncode(toEmail),
                ["CLASS_INFO"] = classInfoBlock,
                ["ORDER_ID"] = WebUtility.HtmlEncode(orderId),
                ["TRANSACTION_ID_ROW"] = transactionIdRow,
                ["PAYMENT_METHOD"] = "MoMo",
                ["DESCRIPTION"] = WebUtility.HtmlEncode(description),
                ["TOTAL_AMOUNT"] = formattedAmount,
                ["YEAR"] = DateTime.Now.Year.ToString()
            });

            await SendAsync(toEmail, $"Hóa đơn thanh toán #{invoiceNumber}", html);
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
