namespace ReenbitEventHub.Application.Exceptions;

public sealed class MessagePublishException(string message, Exception innerException)
    : Exception(message, innerException);
