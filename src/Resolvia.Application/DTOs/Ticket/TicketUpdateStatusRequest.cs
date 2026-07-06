using Resolvia.Domain.Enums;

namespace Resolvia.Application.DTOs.Ticket;

public class TicketUpdateStatusRequest
{
    public TicketStatus NewStatus { get; set; }
}