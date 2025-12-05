using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/admin/system-settings")]
    [Authorize(Roles = "Admin")]
    public class SystemSettingsController : ControllerBase
    {
        private readonly ISystemSettingsService _service;

        public SystemSettingsController(ISystemSettingsService service)
        {
            _service = service;
        }

        /// <summary>
        /// Lấy system settings hiện tại
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSettings(CancellationToken ct)
        {
            var settings = await _service.GetSettingsAsync(ct);
            return Ok(ApiResponse<SystemSettingsDto>.Ok(settings));
        }

        /// <summary>
        /// Cập nhật tỷ lệ tiền cọc (% học phí)
        /// </summary>
        [HttpPut("deposit-settings")]
        public async Task<IActionResult> UpdateDepositSettings([FromBody] UpdateDepositSettingsDto dto, CancellationToken ct)
        {
            var settings = await _service.UpdateDepositSettingsAsync(dto, ct);
            return Ok(ApiResponse<SystemSettingsDto>.Ok(settings, "Cập nhật tỷ lệ tiền cọc thành công"));
        }
    }
}

