using Xunit;
using CarRentalFunction.Contracts;
using CarRentalFunction.Data;
using CarRentalFunction.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarRentalFunction.Tests;

public sealed class EventPersistenceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EventPersistenceServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    // Each test gets its own DbContext (simulates per-invocation DI scope)
    private EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new EventDbContext(options);
    }

    private static EventPersistenceService CreateService(EventDbContext ctx) =>
        new(ctx, NullLogger<EventPersistenceService>.Instance);

    [Fact]
    public async Task PersistAsync_NewEvent_IsSavedToDatabase()
    {
        var message = new EventMessage
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Type = EventType.PageView,
            Description = "landing page visit",
            CreatedAt = DateTime.UtcNow
        };

        using var ctx = CreateContext();
        await CreateService(ctx).PersistAsync(message, CancellationToken.None);

        using var verify = CreateContext();
        var saved = await verify.Events.FindAsync(message.Id);

        Assert.NotNull(saved);
        Assert.Equal(message.UserId, saved.UserId);
        Assert.Equal(message.Type, saved.Type);
        Assert.Equal(message.Description, saved.Description);
    }

    [Fact]
    public async Task PersistAsync_DuplicateId_IsIgnoredWithoutException()
    {
        var message = new EventMessage
        {
            Id = Guid.NewGuid(),
            UserId = "user-2",
            Type = EventType.Click,
            Description = "button click",
            CreatedAt = DateTime.UtcNow
        };

        // First invocation — persists successfully
        using (var ctx1 = CreateContext())
            await CreateService(ctx1).PersistAsync(message, CancellationToken.None);

        // Second invocation with the same Id — must not throw
        using (var ctx2 = CreateContext())
            await CreateService(ctx2).PersistAsync(message, CancellationToken.None);

        // Exactly one row must exist
        using var verify = CreateContext();
        var count = await verify.Events.CountAsync(e => e.Id == message.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PersistAsync_AllThreeEventTypes_ArePersistedCorrectly()
    {
        var messages = new[]
        {
            new EventMessage { Id = Guid.NewGuid(), UserId = "u", Type = EventType.PageView,  Description = "a", CreatedAt = DateTime.UtcNow },
            new EventMessage { Id = Guid.NewGuid(), UserId = "u", Type = EventType.Click,     Description = "b", CreatedAt = DateTime.UtcNow },
            new EventMessage { Id = Guid.NewGuid(), UserId = "u", Type = EventType.Purchase,  Description = "c", CreatedAt = DateTime.UtcNow }
        };

        foreach (var msg in messages)
        {
            using var ctx = CreateContext();
            await CreateService(ctx).PersistAsync(msg, CancellationToken.None);
        }

        using var verify = CreateContext();
        var types = await verify.Events.Select(e => e.Type).ToListAsync();
        Assert.Contains(EventType.PageView, types);
        Assert.Contains(EventType.Click, types);
        Assert.Contains(EventType.Purchase, types);
    }

    [Fact]
    public async Task PersistAsync_DbUnavailable_RethrowsException()
    {
        var message = new EventMessage
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            Type = EventType.Purchase,
            Description = "t",
            CreatedAt = DateTime.UtcNow
        };

        // Isolated closed connection — no schema, simulates DB unavailability
        using var badConn = new SqliteConnection("Data Source=:memory:");
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseSqlite(badConn)
            .Options;
        using var ctx = new EventDbContext(options);

        var ex = await Record.ExceptionAsync(
            () => CreateService(ctx).PersistAsync(message, CancellationToken.None));

        Assert.NotNull(ex);
    }
}
