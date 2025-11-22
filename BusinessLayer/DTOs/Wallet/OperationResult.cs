using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.Wallet
{
    public class OperationResult
    {
        public string Status { get; set; } = "Ok"; // "Ok" | "Fail"
        public string? Message { get; set; }
    }
}