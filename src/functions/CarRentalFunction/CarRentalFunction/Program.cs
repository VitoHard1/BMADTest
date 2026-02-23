using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CarRentalFunction.Data;
using CarRentalFunction.Services;

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Functions__Worker__HostEndpoint")))
{
    // Allow local IDE launches that don't inject Functions host metadata.
    Environment.SetEnvironmentVariable("Functions__Worker__HostEndpoint", "http://127.0.0.1:17071");
}

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Functions__Worker__WorkerId")))
{
    Environment.SetEnvironmentVariable("Functions__Worker__WorkerId", Guid.NewGuid().ToString("N"));
}

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

var eventsDbConnection = builder.Configuration["ConnectionStrings:EventsDb"]
    ?? builder.Configuration["ConnectionStrings__EventsDb"]
    ?? "Data Source=events.db";

builder.Services.AddDbContext<EventDbContext>(options =>
{
    if (eventsDbConnection.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        options.UseSqlServer(eventsDbConnection);
    else
        options.UseSqlite(eventsDbConnection);
});
builder.Services.AddScoped<IEventPersistenceService, EventPersistenceService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EventDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.Run();
