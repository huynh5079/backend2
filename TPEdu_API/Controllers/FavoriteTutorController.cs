using BusinessLayer.DTOs.API;
using BusinessLayer.DTOs.FavoriteTutor;
using BusinessLayer.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TPEdu_API.Common.Extensions;

namespace TPEdu_API.Controllers
{
    [ApiController]
    [Route("tpedu/v1/favorite-tutors")]
    [Authorize(Roles = "Student,Parent")]
    public class FavoriteTutorController : ControllerBase
    {
        private readonly IFavoriteTutorService _service;

        public FavoriteTutorController(IFavoriteTutorService service)
        {
            _service = service;
        }

        /// <summary>
        /// Thêm gia sư vào danh sách yêu thích
        /// </summary>
        [HttpPost("{tutorProfileId}")]
        public async Task<IActionResult> AddFavorite(string tutorProfileId)
        {
            try
            {
                var userId = User.RequireUserId();
                var result = await _service.AddFavoriteAsync(userId, tutorProfileId);
                return Ok(ApiResponse<FavoriteTutorDto>.Ok(result, "Đã thêm gia sư vào danh sách yêu thích"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<object>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<object>.Fail(ex.Message));
            }
        }

        /// <summary>
        /// Xóa gia sư khỏi danh sách yêu thích
        /// </summary>
        [HttpDelete("{tutorProfileId}")]
        public async Task<IActionResult> RemoveFavorite(string tutorProfileId)
        {
            var userId = User.RequireUserId();
            var success = await _service.RemoveFavoriteAsync(userId, tutorProfileId);

            if (!success)
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy gia sư trong danh sách yêu thích"));

            return Ok(ApiResponse<object>.Ok(new { }, "Đã xóa gia sư khỏi danh sách yêu thích"));
        }

        /// <summary>
        /// Lấy danh sách gia sư yêu thích của tôi
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyFavorites()
        {
            var userId = User.RequireUserId();
            var favorites = await _service.GetMyFavoritesAsync(userId);
            return Ok(ApiResponse<object>.Ok(favorites, "Lấy danh sách gia sư yêu thích thành công"));
        }

        /// <summary>
        /// Kiểm tra gia sư đã được favorite chưa
        /// </summary>
        [HttpGet("check/{tutorProfileId}")]
        public async Task<IActionResult> CheckFavorite(string tutorProfileId)
        {
            var userId = User.RequireUserId();
            var isFavorited = await _service.IsFavoritedAsync(userId, tutorProfileId);
            return Ok(ApiResponse<object>.Ok(new { isFavorited }, ""));
        }
    }
}
