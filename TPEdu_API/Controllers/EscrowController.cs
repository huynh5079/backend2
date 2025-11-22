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
    }
}


