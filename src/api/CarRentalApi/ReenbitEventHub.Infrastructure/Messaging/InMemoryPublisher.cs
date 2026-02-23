using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ReenbitEventHub.Application.Services;
using ReenbitEventHub.Domain.DTOs;

namespace ReenbitEventHub.Infrastructure.Messaging;

public sealed class InMemoryPublisher(
    Channel<EventMessage> channel,
    ILogger<InMemoryPublisher> logger) : IMessagePublisher
{
    public async Task<IReadOnlyList<Guid>> PublishEventsAsync(
        IReadOnlyList<EventMessage> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            await channel.Writer.WriteAsync(evt, cancellationToken);
            logger.LogInformation(
                "Published event {EventId} userId={UserId} type={EventType}",
                evt.Id, evt.UserId, evt.Type);
        }

        return events.Select(e => e.Id).ToList();
    }
}
