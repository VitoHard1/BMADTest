using Microsoft.Extensions.Logging.Abstractions;
using ReenbitEventHub.Application.Events;
using ReenbitEventHub.Application.Services;
using ReenbitEventHub.Domain.DTOs;
using ReenbitEventHub.Domain.Entities;
using ReenbitEventHub.Domain.Enums;
using ReenbitEventHub.Domain.Repositories;
using Xunit;

namespace ReenbitEventHub.Application.Tests.Events;

public sealed class EventApplicationServiceTests
{
    [Fact]
    public async Task CreateAsync_ViewCar_PublishesSinglePageViewAndReturnsOneId()
    {
        var repository = new FakeEventRepository();
        var publisher = new FakeMessagePublisher();
        var service = new EventApplicationService(repository, publisher, NullLogger<EventApplicationService>.Instance);

        var request = new CreateEventRequest
        {
            UserId = "user-1",
            Action = EventAction.ViewCar,
            CarId = "car-1"
        };

        var response = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal(1, response.PublishedCount);
        Assert.Single(response.EventIds);

        var message = Assert.Single(publisher.PublishedEvents);
        Assert.Equal(EventType.PageView, message.Type);
        Assert.Equal("user-1", message.UserId);
        Assert.Contains("Viewed car-1 Toyota Corolla", message.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_ReserveCar_PublishesClickAndPurchase()
    {
        var repository = new FakeEventRepository();
        var publisher = new FakeMessagePublisher();
        var service = new EventApplicationService(repository, publisher, NullLogger<EventApplicationService>.Instance);

        var request = new CreateEventRequest
        {
            UserId = "user-2",
            Action = EventAction.ReserveCar,
            CarId = "car-2"
        };

        var response = await service.CreateAsync(request, CancellationToken.None);

        Assert.Equal(2, response.PublishedCount);
        Assert.Equal(2, response.EventIds.Count);
        Assert.Equal(2, publisher.PublishedEvents.Count);
        Assert.Contains(publisher.PublishedEvents, e => e.Type == EventType.Click);
        Assert.Contains(publisher.PublishedEvents, e => e.Type == EventType.Purchase);
    }

    [Fact]
    public async Task GetAsync_InvalidSort_ThrowsArgumentException()
    {
        var repository = new FakeEventRepository();
        var publisher = new FakeMessagePublisher();
        var service = new EventApplicationService(repository, publisher, NullLogger<EventApplicationService>.Instance);

        var query = new GetEventsQueryRequest
        {
            Sort = "desc"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(query, CancellationToken.None));
        Assert.Equal("Sort", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public async Task GetAsync_InvalidPageSize_ThrowsArgumentException(int pageSize)
    {
        var repository = new FakeEventRepository();
        var publisher = new FakeMessagePublisher();
        var service = new EventApplicationService(repository, publisher, NullLogger<EventApplicationService>.Instance);

        var query = new GetEventsQueryRequest
        {
            PageSize = pageSize
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(query, CancellationToken.None));
        Assert.Equal("PageSize", exception.ParamName);
    }

    [Fact]
    public async Task GetAsync_FromGreaterThanTo_ThrowsArgumentException()
    {
        var repository = new FakeEventRepository();
        var publisher = new FakeMessagePublisher();
        var service = new EventApplicationService(repository, publisher, NullLogger<EventApplicationService>.Instance);

        var query = new GetEventsQueryRequest
        {
            From = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.GetAsync(query, CancellationToken.None));
        Assert.Equal("From", exception.ParamName);
    }

    [Fact]
    public async Task GetAsync_ValidRequest_UsesRepositoryParamsAndReturnsMappedResponse()
    {
        var repository = new FakeEventRepository();
        var publisher = new FakeMessagePublisher();
        var service = new EventApplicationService(repository, publisher, NullLogger<EventApplicationService>.Instance);

        repository.QueryResult = (
            new List<Event>
            {
                Event.Reconstitute(
                    Guid.NewGuid(),
                    "user-9",
                    EventType.Click,
                    "Clicked reserve for car-2 VW Golf",
                    new DateTime(2026, 2, 20, 10, 0, 0, DateTimeKind.Utc))
            },
            1);

        var query = new GetEventsQueryRequest
        {
            UserId = "user-9",
            Type = EventType.Click,
            From = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 2, 21, 0, 0, 0, DateTimeKind.Utc),
            Sort = "createdAt_asc",
            Page = 2,
            PageSize = 25
        };

        var response = await service.GetAsync(query, CancellationToken.None);

        Assert.False(repository.LastCreatedAtDescending);
        Assert.Equal("user-9", repository.LastUserId);
        Assert.Equal(EventType.Click, repository.LastType);
        Assert.Equal(2, repository.LastPage);
        Assert.Equal(25, repository.LastPageSize);

        Assert.Single(response.Items);
        Assert.Equal(1, response.TotalCount);
        Assert.Equal(2, response.Page);
        Assert.Equal(25, response.PageSize);
        Assert.Equal("user-9", response.Items.First().UserId);
    }

    private sealed class FakeMessagePublisher : IMessagePublisher
    {
        public List<EventMessage> PublishedEvents { get; } = [];

        public Task<IReadOnlyList<Guid>> PublishEventsAsync(
            IReadOnlyList<EventMessage> events,
            CancellationToken cancellationToken = default)
        {
            PublishedEvents.AddRange(events);
            return Task.FromResult<IReadOnlyList<Guid>>(events.Select(e => e.Id).ToList());
        }
    }

    private sealed class FakeEventRepository : IEventRepository
    {
        public string? LastUserId { get; private set; }
        public EventType? LastType { get; private set; }
        public DateTime? LastFrom { get; private set; }
        public DateTime? LastTo { get; private set; }
        public bool LastCreatedAtDescending { get; private set; }
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }

        public (IReadOnlyCollection<Event> Items, int TotalCount) QueryResult { get; set; } =
            (Array.Empty<Event>(), 0);

        public Task AddAsync(Event entity, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyCollection<Event> Items, int TotalCount)> QueryAsync(
            string? userId,
            EventType? type,
            DateTime? from,
            DateTime? to,
            bool createdAtDescending,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            LastUserId = userId;
            LastType = type;
            LastFrom = from;
            LastTo = to;
            LastCreatedAtDescending = createdAtDescending;
            LastPage = page;
            LastPageSize = pageSize;
            return Task.FromResult(QueryResult);
        }
    }
}
