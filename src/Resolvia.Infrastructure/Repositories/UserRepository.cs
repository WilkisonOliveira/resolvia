using Microsoft.EntityFrameworkCore;
using Resolvia.Application.Interfaces;
using Resolvia.Domain.Entities;
using Resolvia.Infrastructure.Data;

namespace Resolvia.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ResolviaDbContext _context;

    public UserRepository(ResolviaDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}