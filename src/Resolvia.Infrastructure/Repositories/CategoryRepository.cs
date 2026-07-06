using Microsoft.EntityFrameworkCore;
using Resolvia.Application.Interfaces;
using Resolvia.Domain.Entities;
using Resolvia.Infrastructure.Data;

namespace Resolvia.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ResolviaDbContext _context;

    public CategoryRepository(ResolviaDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetAllAsync()
    {
        return await _context.Categories.AsNoTracking().ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(Guid id)
    {
        return await _context.Categories.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
    }

    public void Update(Category category)
    {
        _context.Categories.Update(category);
    }

    public void Delete(Category category)
    {
        _context.Categories.Remove(category);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}