using Microsoft.EntityFrameworkCore;
using Resolvia.Application.Interfaces;
using Resolvia.Domain.Entities;
using Resolvia.Infrastructure.Data;

namespace Resolvia.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly ResolviaDbContext _context;

    public TicketRepository(ResolviaDbContext context)
    {
        _context = context;
    }

    private IQueryable<Ticket> TicketsWithRelations() =>
        _context.Tickets
            .Include(t => t.Category)
            .Include(t => t.Requester)
            .Include(t => t.AssignedTo);

    public async Task<List<Ticket>> GetAllAsync()
    {
        return await TicketsWithRelations().AsNoTracking().ToListAsync();
    }

    public async Task<List<TicketHistory>> GetHistoryByTicketIdAsync(Guid ticketId)
    {
        return await _context.TicketHistories
            .Include(h => h.ChangedByUser)
            .Where(h => h.TicketId == ticketId)
            .OrderBy(h => h.ChangedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Ticket>> GetByRequesterIdAsync(Guid requesterId)
    {
        return await TicketsWithRelations()
            .AsNoTracking()
            .Where(t => t.RequesterId == requesterId)
            .ToListAsync();
    }

    public async Task AddCommentAsync(TicketComment comment)
    {
        await _context.TicketComments.AddAsync(comment);
    }

    public async Task<List<TicketComment>> GetCommentsByTicketIdAsync(Guid ticketId, bool includeInternal)
    {
        var query = _context.TicketComments
            .Include(c => c.User)
            .Where(c => c.TicketId == ticketId);

        if (!includeInternal)
            query = query.Where(c => !c.IsInternal);

        return await query
            .OrderBy(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task AddAttachmentAsync(TicketAttachment attachment)
    {
        await _context.TicketAttachments.AddAsync(attachment);
    }

    public async Task<List<TicketAttachment>> GetAttachmentsByTicketIdAsync(Guid ticketId)
    {
        return await _context.TicketAttachments
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.UploadedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Ticket?> GetByIdAsync(Guid id)
    {
        return await TicketsWithRelations().FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task AddAsync(Ticket ticket)
    {
        await _context.Tickets.AddAsync(ticket);
    }

    public void Update(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
    }

    public async Task AddHistoryAsync(TicketHistory history)
    {
        await _context.TicketHistories.AddAsync(history);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}