using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.DTOs.API
{
    public class ApiResponse<T>
    {
        public string Status { get; set; } = "Success";
        public string Message { get; set; } = "";
        public T? Data { get; set; }

        // Dùng các static method để tạo response nhanh hơn trong controller
        public static ApiResponse<T> Ok(T data, string message = "")
        {
            return new ApiResponse<T> { Status = "Success", Data = data, Message = message };
        }

        public static ApiResponse<T> Fail(string message)
        {
            return new ApiResponse<T> { Status = "Fail", Message = message, Data = default };
        }
    }
}
