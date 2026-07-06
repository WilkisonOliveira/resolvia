using Resolvia.Domain.Entities;

namespace Resolvia.Application.Interfaces;

public interface ITicketRepository
{
    Task<List<Ticket>> GetAllAsync();
    Task<List<Ticket>> GetByRequesterIdAsync(Guid requesterId);
    Task<Ticket?> GetByIdAsync(Guid id);
    Task AddAsync(Ticket ticket);
    void Update(Ticket ticket);
    Task AddHistoryAsync(TicketHistory history);
    Task<List<TicketHistory>> GetHistoryByTicketIdAsync(Guid ticketId);
    Task SaveChangesAsync();
    Task AddCommentAsync(TicketComment comment);
    Task<List<TicketComment>> GetCommentsByTicketIdAsync(Guid ticketId, bool includeInternal);
    Task AddAttachmentAsync(TicketAttachment attachment);
    Task<List<TicketAttachment>> GetAttachmentsByTicketIdAsync(Guid ticketId);
}