using Microsoft.EntityFrameworkCore;
using Resolvia.Domain.Entities;

namespace Resolvia.Infrastructure.Data;

public class ResolviaDbContext : DbContext
{
    public ResolviaDbContext(DbContextOptions<ResolviaDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResolviaDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}