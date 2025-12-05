using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/escrow")]
    [Authorize]
    public class EscrowController : ControllerBase
    {
        private readonly IEscrowService _svc;
        public EscrowController(IEscrowService svc) { _svc = svc; }

        [HttpPost("pay")]
        [Authorize(Roles = "Student,Parent,Admin")]
        public async Task<IActionResult> Pay([FromBody] PayEscrowRequest req)
        {
            var userId = User.RequireUserId();
            var res = await _svc.PayEscrowAsync(userId, req);
            if (res.Status == "Fail") return BadRequest(res);
            return Ok(res);
        }

        [HttpPost("{id}/release")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Release(string id)
        {
            var userId = User.RequireUserId();
            var res = await _svc.ReleaseAsync(userId, new ReleaseEscrowRequest { EscrowId = id });
            if (res.Status == "Fail") return BadRequest(res);
            return Ok(res);
        }

        [HttpPost("{id}/refund")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Refund(string id)
        {
            var userId = User.RequireUserId();
            var res = await _svc.RefundAsync(userId, new RefundEscrowRequest { EscrowId = id });
            if (res.Status == "Fail") return BadRequest(res);
            return Ok(res);
        }

        /// <summary>
        /// Tính toán commission trước khi thanh toán
        /// Commission được tính dựa trên loại lớp học (1-1/nhóm, Online/Offline)
        /// - OneToOneOnline: 12%
        /// - OneToOneOffline: 15%
        /// - GroupClassOnline: 10%
        /// - GroupClassOffline: 12%
        /// GrossAmount sẽ tự động lấy từ Class.Price trong database (không cần truyền vào)
        /// </summary>
        [HttpGet("calculate-commission")]
        [Authorize(Roles = "Student,Parent,Admin")]
        public async Task<IActionResult> CalculateCommission([FromQuery] string classId, [FromQuery] decimal? grossAmount = null)
        {
            try
            {
                var result = await _svc.CalculateCommissionAsync(classId, grossAmount);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// [Tutor] Đặt cọc khi chấp nhận lớp
        /// Gia sư phải đặt cọc (10% học phí) trước khi nhận lớp
        /// </summary>
        [HttpPost("tutor-deposit")]
        [Authorize(Roles = "Tutor")]
        public async Task<IActionResult> ProcessTutorDeposit([FromBody] ProcessTutorDepositRequest req)
        {
            var tutorUserId = User.RequireUserId();
            var res = await _svc.ProcessTutorDepositAsync(tutorUserId, req);
            if (res.Status == "Fail") return BadRequest(res);
            return Ok(res);
        }

        /// <summary>
        /// [Admin] Tịch thu tiền cọc khi gia sư vi phạm/bỏ dở
        /// </summary>
        [HttpPost("forfeit-deposit")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ForfeitDeposit([FromBody] ForfeitDepositRequest req)
        {
            var adminUserId = User.RequireUserId();
            var res = await _svc.ForfeitDepositAsync(adminUserId, req);
            if (res.Status == "Fail") return BadRequest(res);
            return Ok(res);
        }
    }
}


