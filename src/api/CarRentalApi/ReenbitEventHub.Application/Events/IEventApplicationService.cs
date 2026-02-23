namespace ReenbitEventHub.Application.Events;

public interface IEventApplicationService
{
    Task<CreateEventResponse> CreateAsync(CreateEventRequest request, CancellationToken cancellationToken);
    Task<GetEventsResponse> GetAsync(GetEventsQueryRequest query, CancellationToken cancellationToken);
}
