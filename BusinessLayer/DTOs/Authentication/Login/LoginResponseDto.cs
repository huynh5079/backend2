namespace BusinessLayer.DTOs.Authentication.Login;

public class LoginResponseDto
{
    public string Token { get; set; } = default!;
    public LoggedInUserDto User { get; set; } = default!;
}