using System.Net.Sockets;

namespace Resolvia.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DefaultSlaHours { get; set; } = 24;

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}