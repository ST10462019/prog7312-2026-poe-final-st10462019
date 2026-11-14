using SmartX.PoE.Api.Models;
using SmartX.PoE.Api.Services;

namespace SmartX.PoE.Api.Endpoints;

public static class TopologyEndpoints
{
    public static IEndpointRouteBuilder MapTopologyEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/topology").WithTags("Topology");

        group.MapGet("/", (TopologyService topology) => Results.Ok(topology.GetTopology()));

        group.MapGet("/nodes/{idOrMac}/route", (string idOrMac, TopologyService topology) =>
            topology.TryGetNode(idOrMac, out _)
                ? Results.Ok(topology.GetRoute(idOrMac))
                : Results.NotFound(new { error = $"No node with id or MAC '{idOrMac}'." }));

        group.MapGet("/reachable", (string? @from, string? subnet, TopologyService topology) =>
            string.IsNullOrWhiteSpace(@from) || topology.TryGetNode(@from, out _)
                ? Results.Ok(topology.GetReachability(@from, subnet))
                : Results.NotFound(new { error = $"No node with id or MAC '{@from}'." }));

        group.MapGet("/failures", (TopologyService topology) => Results.Ok(topology.GetFailures()));

        group.MapPost("/nodes/{idOrMac}/status", (string idOrMac, StatusRequest request, TopologyService topology) =>
        {
            if (!topology.TryGetNode(idOrMac, out _))
                return Results.NotFound(new { error = $"No node with id or MAC '{idOrMac}'." });

            if (!Enum.TryParse(request.Status, ignoreCase: true, out NodeStatus status))
                return Results.BadRequest(new { error = "Status must be 'Online' or 'Offline'." });

            try
            {
                MeshNode node = topology.SetStatus(idOrMac, status);
                return Results.Ok(new { node = node.Id, status = node.Status.ToString(), report = topology.GetFailures() });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
