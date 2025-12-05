using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TPEdu_API.Controllers
{
    [Route("api/test-gemini")]
    [ApiController]
    public class TestGeminiController : ControllerBase
    {
        [HttpGet("list")]
        public async Task<IActionResult> ListModels([FromQuery] string apiKey)
        {
            // Gọi vào endpoint gốc để liệt kê tất cả model khả dụng
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            var jsonString = await response.Content.ReadAsStringAsync();

            return Ok(new
            {
                StatusCode = response.StatusCode,
                AvailableModels = jsonString
            });
        }
    }
}
