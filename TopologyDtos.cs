namespace SmartX.PoE.Api.Models;

public sealed record NodeDto(
    string Id,
    string Mac,
    string Name,
    string Subnet,
    bool IsGateway,
    string Status,
    bool Reachable,
    int? Hops,
    int LayoutDepth);

public sealed record LinkDto(string From, string To, double Cost, bool InMst, bool Active);

public sealed record TopologySummary(int Total, int Online, int Offline, int Isolated, int MstEdges, double MstCost);

public sealed record TopologyDto(
    string Gateway,
    IReadOnlyList<NodeDto> Nodes,
    IReadOnlyList<LinkDto> Links,
    TopologySummary Summary);

public sealed record RouteView(IReadOnlyList<string> Hops, double TotalCost, int HopCount);

public sealed record RouteDto(
    string Id,
    string Mac,
    string Name,
    string Status,
    bool Reachable,
    RouteView? MstRoute,
    RouteView? CheapestRoute,
    RouteView? FewestHopsRoute,
    RouteView? LastKnownRoute,
    string Message);

public sealed record ReachableNodeDto(string Id, string Mac, string Name, string Subnet, int Hops);

public sealed record ReachabilityDto(
    string From,
    string? Subnet,
    IReadOnlyList<ReachableNodeDto> Reachable,
    IReadOnlyList<NodeDto> Unreachable);

public sealed record AffectedNodeDto(
    string Id,
    string Name,
    IReadOnlyList<string> OldRoute,
    IReadOnlyList<string>? NewRoute,
    double? NewCost,
    string? NextHop,
    bool Isolated);

public sealed record FailureDto(
    string Id,
    string Mac,
    string Name,
    IReadOnlyList<string> RerouteVia,
    IReadOnlyList<AffectedNodeDto> Affected,
    string Advice);

public sealed record FailureReportDto(IReadOnlyList<FailureDto> Failures, IReadOnlyList<NodeDto> Isolated);

public sealed record StatusRequest(string Status);

public sealed record TelemetryIngestRequest(
    string SensorId,
    string Metric,
    double Value,
    string? Unit,
    DateTimeOffset? Timestamp);

public sealed record TelemetrySummary(
    string SensorId,
    int Count,
    DateTimeOffset? Earliest,
    DateTimeOffset? Latest,
    int TreeHeight);
