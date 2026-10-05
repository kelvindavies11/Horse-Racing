using HorseRacing.Application.Races.CreateRace;
using HorseRacing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CreateRaceHandler>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapPost(
    "/api/races",
    async (
        CreateRaceRequest request,
        CreateRaceHandler handler,
        CancellationToken cancellationToken) =>
    {
        var raceId = await handler.HandleAsync(
            new CreateRaceCommand(
                request.RacecourseId,
                request.Name,
                request.ScheduledStartUtc),
            cancellationToken);

        return Results.Created($"/api/races/{raceId}", new { id = raceId });
    });

app.Run();

public sealed record CreateRaceRequest(
    Guid RacecourseId,
    string Name,
    DateTimeOffset ScheduledStartUtc);

public partial class Program;
