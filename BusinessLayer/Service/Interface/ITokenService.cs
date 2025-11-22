using DataLayer.Entities;

namespace BusinessLayer.Service.Interface;

public interface ITokenService
{
    string CreateToken(User user);
}