using CarRentalApi.BackgroundServices;
using CarRentalApi.Services;
using Microsoft.AspNetCore.Diagnostics;
using ReenbitEventHub.Application;
using ReenbitEventHub.Application.Exceptions;
using ReenbitEventHub.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .WithMethods("GET", "POST", "OPTIONS")
            .WithHeaders("Content-Type", "Authorization");
    });
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IEventPersistenceService, EventPersistenceService>();

// In local mode (no Service Bus connection string) use the in-process BackgroundService consumer
if (string.IsNullOrWhiteSpace(builder.Configuration["ServiceBus:ConnectionString"]))
{
    builder.Services.AddHostedService<EventProcessorService>();
}
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

await app.Services.InitializeInfrastructureAsync();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is ArgumentException argumentException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [string.IsNullOrWhiteSpace(argumentException.ParamName) ? "request" : argumentException.ParamName] =
                    [argumentException.Message]
            }).ExecuteAsync(context);
            return;
        }

        if (exception is MessagePublishException publishException)
        {
            var logger = context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("ExceptionHandler");
            logger.LogError(publishException,
                "Message publish failed: {Message}", publishException.Message);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable",
                detail: "Failed to publish events to the message queue.",
                extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }
            ).ExecuteAsync(context);
            return;
        }

        var unhandledLogger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ExceptionHandler");
        if (exception is not null)
        {
            unhandledLogger.LogError(exception, "Unhandled exception occurred.");
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "Internal Server Error",
            detail: "An unexpected error occurred.",
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }
        ).ExecuteAsync(context);
    });
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("FrontendDev");
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
