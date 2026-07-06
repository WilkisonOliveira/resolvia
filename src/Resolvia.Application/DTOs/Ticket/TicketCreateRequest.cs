using Resolvia.Domain.Enums;

namespace Resolvia.Application.DTOs.Ticket;

public class TicketCreateRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public TicketPriority Priority { get; set; }
}