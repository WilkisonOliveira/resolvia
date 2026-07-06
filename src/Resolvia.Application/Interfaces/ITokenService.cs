using Resolvia.Domain.Entities;

namespace Resolvia.Application.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}