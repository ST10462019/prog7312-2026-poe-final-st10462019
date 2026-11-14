using SmartX.PoE.Api.Models;
using SmartX.PoE.Api.Services;

namespace SmartX.PoE.Api.Endpoints;

public static class TelemetryEndpoints
{
    public static IEndpointRouteBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/telemetry").WithTags("Telemetry");

      
        group.MapGet("/{idOrMac}", (
            string idOrMac,
            DateTimeOffset? @from,
            DateTimeOffset? to,
            string? metric,
            int? limit,
            TopologyService topology,
            TelemetryStore store) =>
        {
            if (!topology.TryGetNode(idOrMac, out MeshNode? node))
                return Results.NotFound(new { error = $"No node with id or MAC '{idOrMac}'." });

            DateTimeOffset end = to ?? DateTimeOffset.UtcNow;
            DateTimeOffset start = @from ?? end.AddHours(-2);
            if (start > end)
                return Results.BadRequest(new { error = "'from' must not be later than 'to'." });

            int size = Math.Clamp(limit ?? 500, 1, 5000);
            return Results.Ok(store.Query(node.Id, start, end, metric, size));
        });

  
        group.MapGet("/{idOrMac}/summary", (string idOrMac, TopologyService topology, TelemetryStore store) =>
            topology.TryGetNode(idOrMac, out MeshNode? node)
                ? Results.Ok(store.Summarise(node.Id))
                : Results.NotFound(new { error = $"No node with id or MAC '{idOrMac}'." }));


        group.MapPost("/", (TelemetryIngestRequest request, TopologyService topology, TelemetryStore store) =>
        {
            if (!topology.TryGetNode(request.SensorId, out MeshNode? node))
                return Results.NotFound(new { error = $"No node with id or MAC '{request.SensorId}'." });

            if (string.IsNullOrWhiteSpace(request.Metric))
                return Results.BadRequest(new { error = "A metric name is required." });

            var reading = new TelemetryReading(
                node.Id,
                request.Timestamp ?? DateTimeOffset.UtcNow,
                request.Metric.Trim().ToLowerInvariant(),
                request.Value,
                request.Unit ?? string.Empty);

            store.Add(reading);
            return Results.Created($"/api/telemetry/{node.Id}", reading);
        });

        return app;
    }
}
