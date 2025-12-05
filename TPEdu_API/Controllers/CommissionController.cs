using BusinessLayer.DTOs.Wallet;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers;

[ApiController]
[Route("tpedu/v1/admin/commission")]
[Authorize(Roles = "Admin")]
public class CommissionController : ControllerBase
{
    private readonly ICommissionManagementService _service;

    public CommissionController(ICommissionManagementService service)
    {
        _service = service;
    }

    /// <summary>
    /// Lấy commission settings hiện tại
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCommission()
    {
        var commission = await _service.GetCommissionAsync();
        return Ok(commission);
    }

    /// <summary>
    /// Cập nhật commission settings
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdateCommission([FromBody] UpdateCommissionDto dto)
    {
        var commission = await _service.UpdateCommissionAsync(dto);
        return Ok(commission);
    }
}

