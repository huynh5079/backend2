namespace BusinessLayer.DTOs.Authentication.Login;

public class LoginRequestDto
{
    public string Email { get; set; } = default!;
    public string Password { get; set; }  = default!;
}