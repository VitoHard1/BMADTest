using Xunit;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CarRentalFunction.Contracts;
using CarRentalFunction.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CarRentalFunction.Tests;

public sealed class EventProcessorFunctionTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Mock<IEventPersistenceService> _persistenceMock = new();
    private readonly EventProcessorFunction _function;

    public EventProcessorFunctionTests()
    {
        _function = new EventProcessorFunction(
            _persistenceMock.Object,
            NullLogger<EventProcessorFunction>.Instance);
    }

    private static ServiceBusReceivedMessage BuildMessage(EventMessage payload) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromObjectAsJson(payload, CamelCase));

    [Fact]
    public async Task Run_ValidMessage_CallsPersistAsyncOnce()
    {
        var payload = new EventMessage
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Type = EventType.Purchase,
            Description = "car booked",
            CreatedAt = DateTime.UtcNow
        };

        await _function.Run(BuildMessage(payload), CancellationToken.None);

        _persistenceMock.Verify(
            s => s.PersistAsync(
                It.Is<EventMessage>(m => m.Id == payload.Id && m.UserId == payload.UserId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_NullJsonBody_ThrowsJsonException()
    {
        // "null" deserializes to null reference — triggers the null guard
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("null"));

        await Assert.ThrowsAsync<JsonException>(
            () => _function.Run(message, CancellationToken.None));
    }

    [Fact]
    public async Task Run_MalformedBody_ThrowsJsonException()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{not valid json}"));

        await Assert.ThrowsAsync<JsonException>(
            () => _function.Run(message, CancellationToken.None));
    }

    [Fact]
    public async Task Run_PersistFails_RethrowsOriginalException()
    {
        var expected = new InvalidOperationException("DB unavailable");

        _persistenceMock
            .Setup(s => s.PersistAsync(It.IsAny<EventMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var payload = new EventMessage
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            Type = EventType.PageView,
            Description = "d",
            CreatedAt = DateTime.UtcNow
        };

        var actual = await Record.ExceptionAsync(
            () => _function.Run(BuildMessage(payload), CancellationToken.None));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Run_ValidMessage_PassesCancellationTokenToPersist()
    {
        var cts = new CancellationTokenSource();
        var payload = new EventMessage
        {
            Id = Guid.NewGuid(),
            UserId = "u",
            Type = EventType.Click,
            Description = "d",
            CreatedAt = DateTime.UtcNow
        };

        await _function.Run(BuildMessage(payload), cts.Token);

        _persistenceMock.Verify(
            s => s.PersistAsync(It.IsAny<EventMessage>(), cts.Token),
            Times.Once);
    }
}
