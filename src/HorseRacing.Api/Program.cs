using HorseRacing.Application.Races.CreateRace;
using HorseRacing.Api;
using HorseRacing.Domain.Enums;
using HorseRacing.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<CreateRaceHandler>();
builder.Services.AddSingleton<ImportControlService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapCuratedReadEndpoints();
app.MapImportControlEndpoints();

app.MapPost(
    "/api/races",
    async (
        CreateRaceRequest request,
        CreateRaceHandler handler,
        CancellationToken cancellationToken) =>
    {
        var raceId = await handler.HandleAsync(
            new CreateRaceCommand(
                request.MeetingId,
                request.RaceNumber,
                request.Name,
                request.ScheduledStartUtc,
                request.Code,
                request.Surface,
                request.DistanceMetres),
            cancellationToken);

        return Results.Created($"/api/races/{raceId}", new { id = raceId });
    });

app.Run();

public sealed record CreateRaceRequest(
    Guid MeetingId,
    int RaceNumber,
    string Name,
    DateTimeOffset ScheduledStartUtc,
    RaceCode Code,
    RacingSurface Surface,
    int DistanceMetres);

public partial class Program;
