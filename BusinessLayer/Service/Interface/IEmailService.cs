using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Service.Interface
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string toEmail, string otpCode);
        Task SendAsync(string toEmail, string subject, string htmlBody);
        Task SendInvoiceEmailAsync(
            string toEmail,
            string customerName,
            string invoiceNumber,
            string orderId,
            string? transactionId,
            decimal amount,
            string description,
            string? classTitle = null,
            string? classSubject = null);
    }
}
