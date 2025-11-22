namespace BusinessLayer.DTOs.Authentication.Login;

public class LoggedInUserDto
{
    public string Id { get; set; }
    public string Username { get; set; } = default!;
    public string? AvatarUrl { get; set; }  // hiện chưa có cột -> trả null
    public string Role { get; set; } = default!;
}