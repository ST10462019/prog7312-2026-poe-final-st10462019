using System.Diagnostics.CodeAnalysis;
using SmartX.PoE.Api.DataStructures;
using SmartX.PoE.Api.Models;

namespace SmartX.PoE.Api.Services;

public sealed class TopologyService
{
    private sealed record Snapshot(
        MstResult Baseline,      
        MstResult Healed,         
        BfsResult Reach,        
        BfsResult Layout,         
        ShortestPathTree Cheapest);

    private readonly MeshGraph _graph = new();
    private readonly object _gate = new();
    private string _gatewayId = string.Empty;
    private int _version;
    private int _cachedVersion = -1;
    private Snapshot? _cache;

    public string GatewayId => _gatewayId;


    public void AddNode(MeshNode node)
    {
        lock (_gate)
        {
            _graph.AddNode(node);
            if (node.IsGateway) _gatewayId = node.Id;
            _version++;
        }
    }

    public void AddLink(string idA, string idB, double cost)
    {
        lock (_gate)
        {
            _graph.AddLink(idA, idB, cost);
            _version++;
        }
    }

    public bool TryGetNode(string idOrMac, [NotNullWhen(true)] out MeshNode? node)
    {
        lock (_gate)
        {
            bool found = _graph.TryResolve(idOrMac, out MeshNode resolved);
            node = found ? resolved : null;
            return found;
        }
    }

    public MeshNode SetStatus(string idOrMac, NodeStatus status)
    {
        lock (_gate)
        {
            MeshNode node = _graph.GetNode(idOrMac);
            if (node.IsGateway) throw new InvalidOperationException("The gateway cannot be taken offline.");
            if (node.Status != status)
            {
                node.Status = status;
                _version++;
            }

            return node;
        }
    }


    public TopologyDto GetTopology()
    {
        lock (_gate)
        {
            Snapshot s = Snap();

            var inMst = new HashSet<(string, string)>(s.Healed.Edges.Select(e => Pair(e.From, e.To)));

            List<NodeDto> nodes = _graph.Nodes.OrderBy(n => n.Id, StringComparer.Ordinal).Select(n => ToDto(n, s)).ToList();

            List<LinkDto> links = _graph.Links()
                .OrderBy(e => e.From, StringComparer.Ordinal)
                .ThenBy(e => e.To, StringComparer.Ordinal)
                .Select(e => new LinkDto(
                    e.From,
                    e.To,
                    e.Cost,
                    inMst.Contains(Pair(e.From, e.To)),
                    _graph.GetNode(e.From).IsOnline && _graph.GetNode(e.To).IsOnline))
                .ToList();

            int online = nodes.Count(n => n.Status == nameof(NodeStatus.Online));
            int isolated = nodes.Count(n => n.Status == nameof(NodeStatus.Online) && !n.Reachable);

            var summary = new TopologySummary(
                nodes.Count,
                online,
                nodes.Count - online,
                isolated,
                s.Healed.Edges.Count,
                Math.Round(s.Healed.TotalCost, 2));

            return new TopologyDto(_gatewayId, nodes, links, summary);
        }
    }

    public RouteDto GetRoute(string idOrMac)
    {
        lock (_gate)
        {
            Snapshot s = Snap();
            MeshNode node = _graph.GetNode(idOrMac);

            RouteView? mst = ToView(MeshAlgorithms.RouteToRoot(s.Healed.ParentEdge, _gatewayId, node.Id));
            RouteView? cheapest = ToView(MeshAlgorithms.RouteToRoot(s.Cheapest.ParentEdge, _gatewayId, node.Id));
            RouteView? fewest = ToView(MeshAlgorithms.RouteToRoot(s.Reach, _graph, node.Id));
            RouteView? lastKnown = ToView(MeshAlgorithms.RouteToRoot(s.Baseline.ParentEdge, _gatewayId, node.Id));

            string message;
            if (!node.IsOnline)
            {
                message = $"{node.Name} is offline, so it cannot send data. Its last known route is shown for reference.";
            }
            else if (mst is null)
            {
                message = $"{node.Name} is online but cut off: every path to the gateway passes through an offline node.";
            }
            else if (cheapest is not null && mst.TotalCost > cheapest.TotalCost + 1e-9)
            {
                message = $"The MST route costs {mst.TotalCost:0.0}. A dedicated cheapest path would cost {cheapest.TotalCost:0.0}, " +
                          "but the MST minimises the energy of the whole network rather than one route.";
            }
            else
            {
                message = $"{node.Name} reaches the gateway in {mst.HopCount} hop(s) with a total cost of {mst.TotalCost:0.0}.";
            }

            return new RouteDto(
                node.Id,
                node.Mac,
                node.Name,
                node.Status.ToString(),
                mst is not null,
                mst,
                cheapest,
                fewest,
                node.IsOnline ? null : lastKnown,
                message);
        }
    }

    public ReachabilityDto GetReachability(string? fromIdOrMac, string? subnet)
    {
        lock (_gate)
        {
            Snapshot s = Snap();
            MeshNode from = string.IsNullOrWhiteSpace(fromIdOrMac) ? _graph.GetNode(_gatewayId) : _graph.GetNode(fromIdOrMac);

            BfsResult bfs = from.Id == _gatewayId
                ? s.Reach
                : from.IsOnline
                    ? MeshAlgorithms.Bfs(_graph, from.Id, n => n.IsOnline)
                    : new BfsResult(from.Id, Array.Empty<string>(), new Dictionary<string, int>(), new Dictionary<string, string>());

            bool InSubnet(MeshNode n) =>
                string.IsNullOrWhiteSpace(subnet) || string.Equals(n.Subnet, subnet, StringComparison.OrdinalIgnoreCase);

            List<ReachableNodeDto> reachable = bfs.Order
                .Select(id => _graph.GetNode(id))
                .Where(InSubnet)
                .Select(n => new ReachableNodeDto(n.Id, n.Mac, n.Name, n.Subnet, bfs.Depth[n.Id]))
                .ToList();

            List<NodeDto> unreachable = _graph.Nodes
                .Where(n => InSubnet(n) && !bfs.Depth.ContainsKey(n.Id))
                .OrderBy(n => n.Id, StringComparer.Ordinal)
                .Select(n => ToDto(n, s))
                .ToList();

            return new ReachabilityDto(from.Id, string.IsNullOrWhiteSpace(subnet) ? null : subnet, reachable, unreachable);
        }
    }

    public FailureReportDto GetFailures()
    {
        lock (_gate)
        {
            Snapshot s = Snap();
            Dictionary<string, List<string>> children = MeshAlgorithms.ChildrenOf(s.Baseline);
            var failures = new List<FailureDto>();

            foreach (MeshNode down in _graph.Nodes.Where(n => !n.IsOnline).OrderBy(n => n.Id, StringComparer.Ordinal))
            {
                var affected = new List<AffectedNodeDto>();

                foreach (string id in MeshAlgorithms.DfsSubtree(children, down.Id).Where(id => id != down.Id))
                {
                    MeshNode dependant = _graph.GetNode(id);
                    if (!dependant.IsOnline) continue;

                    RoutePath oldRoute = MeshAlgorithms.RouteToRoot(s.Baseline.ParentEdge, _gatewayId, id)!;
                    RoutePath? newRoute = MeshAlgorithms.RouteToRoot(s.Healed.ParentEdge, _gatewayId, id);
                    string? nextHop = newRoute is { Hops.Count: > 1 } ? newRoute.Hops[1] : null;

                    affected.Add(new AffectedNodeDto(
                        id,
                        dependant.Name,
                        oldRoute.Hops,
                        newRoute?.Hops,
                        newRoute?.TotalCost,
                        nextHop,
                        newRoute is null));
                }

                List<string> rerouteVia = _graph.Edges(down.Id)
                    .Select(e => e.To)
                    .Where(id => _graph.GetNode(id).IsOnline && s.Healed.Reached.Contains(id))
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList();

                failures.Add(new FailureDto(down.Id, down.Mac, down.Name, rerouteVia, affected, Advise(down, affected, rerouteVia)));
            }

            List<NodeDto> isolated = _graph.Nodes
                .Where(n => n.IsOnline && !s.Reach.Depth.ContainsKey(n.Id))
                .OrderBy(n => n.Id, StringComparer.Ordinal)
                .Select(n => ToDto(n, s))
                .ToList();

            return new FailureReportDto(failures, isolated);
        }
    }


    private Snapshot Snap()
    {
        if (string.IsNullOrEmpty(_gatewayId)) throw new InvalidOperationException("The mesh has no gateway yet.");
        if (_cache is not null && _cachedVersion == _version) return _cache;

        _cache = new Snapshot(
            MeshAlgorithms.Prim(_graph, _gatewayId),
            MeshAlgorithms.Prim(_graph, _gatewayId, n => n.IsOnline),
            MeshAlgorithms.Bfs(_graph, _gatewayId, n => n.IsOnline),
            MeshAlgorithms.Bfs(_graph, _gatewayId),
            MeshAlgorithms.Dijkstra(_graph, _gatewayId, n => n.IsOnline));
        _cachedVersion = _version;
        return _cache;
    }

    private static NodeDto ToDto(MeshNode node, Snapshot s)
    {
        bool reachable = node.IsOnline && s.Reach.Depth.ContainsKey(node.Id);
        int fallbackDepth = s.Layout.Depth.Count == 0 ? 1 : s.Layout.Depth.Values.Max() + 1;
        int layoutDepth = s.Layout.Depth.TryGetValue(node.Id, out int d) ? d : fallbackDepth;

        return new NodeDto(
            node.Id,
            node.Mac,
            node.Name,
            node.Subnet,
            node.IsGateway,
            node.Status.ToString(),
            reachable,
            reachable ? s.Reach.Depth[node.Id] : null,
            layoutDepth);
    }

    private static RouteView? ToView(RoutePath? path) =>
        path is null ? null : new RouteView(path.Hops, Math.Round(path.TotalCost, 2), path.Hops.Count - 1);

    private static (string, string) Pair(string a, string b) =>
        string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);

    private static string Advise(MeshNode down, List<AffectedNodeDto> affected, List<string> rerouteVia)
    {
        if (affected.Count == 0)
            return $"No traffic depended on {down.Name}, so nothing needs to re-route.";

        int cutOff = affected.Count(a => a.Isolated);
        string via = rerouteVia.Count == 0 ? "no online neighbours" : string.Join(", ", rerouteVia);
        string rerouted = affected.Count - cutOff > 0
            ? $"{affected.Count - cutOff} node(s) re-route automatically. "
            : string.Empty;
        string stranded = cutOff > 0
            ? $"{cutOff} node(s) are cut off and need a relay or a repaired {down.Name}. "
            : string.Empty;

        return $"{rerouted}{stranded}Online neighbours of {down.Name}: {via}.";
    }
}
