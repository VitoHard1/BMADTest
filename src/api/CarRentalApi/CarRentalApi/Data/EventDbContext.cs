using Microsoft.EntityFrameworkCore;
using ReenbitEventHub.Domain.Entities;
using ReenbitEventHub.Domain.Enums;

namespace CarRentalApi.Data;

public class EventDbContext(DbContextOptions<EventDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion(
                    v => v.ToString(),
                    v => Enum.Parse<EventType>(v));

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Events_CreatedAt");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                .HasDatabaseName("IX_Events_UserId_CreatedAt");

            entity.HasIndex(e => new { e.Type, e.CreatedAt })
                .HasDatabaseName("IX_Events_Type_CreatedAt");
        });
    }
}
