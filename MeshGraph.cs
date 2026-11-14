using SmartX.PoE.Api.Models;

namespace SmartX.PoE.Api.DataStructures;

public sealed class MeshGraph
{
    private readonly Dictionary<string, MeshNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _macToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MeshEdge>> _adjacency = new(StringComparer.Ordinal);

    public int NodeCount => _nodes.Count;

    public int LinkCount => _adjacency.Values.Sum(list => list.Count) / 2;

    public IEnumerable<MeshNode> Nodes => _nodes.Values;

    public MeshNode AddNode(MeshNode node)
    {
        if (_nodes.ContainsKey(node.Id)) throw new ArgumentException($"Node '{node.Id}' already exists.");
        string mac = NormaliseMac(node.Mac);
        if (_macToId.ContainsKey(mac)) throw new ArgumentException($"MAC '{mac}' is already registered.");

        _nodes[node.Id] = node;
        _macToId[mac] = node.Id;
        _adjacency[node.Id] = new List<MeshEdge>();
        return node;
    }

    public void AddLink(string idA, string idB, double cost)
    {
        if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost), "Link cost cannot be negative.");
        MeshNode a = GetNode(idA);
        MeshNode b = GetNode(idB);
        if (a.Id == b.Id) throw new ArgumentException("A node cannot link to itself.");
        if (_adjacency[a.Id].Any(e => e.To == b.Id)) throw new ArgumentException($"Link {a.Id}-{b.Id} already exists.");

        _adjacency[a.Id].Add(new MeshEdge(a.Id, b.Id, cost));
        _adjacency[b.Id].Add(new MeshEdge(b.Id, a.Id, cost));
    }

    public bool TryResolve(string idOrMac, out MeshNode node)
    {
        node = null!;
        if (string.IsNullOrWhiteSpace(idOrMac)) return false;

        string key = idOrMac.Trim();
        if (_nodes.TryGetValue(key, out MeshNode? byId))
        {
            node = byId;
            return true;
        }

        if (_macToId.TryGetValue(NormaliseMac(key), out string? id))
        {
            node = _nodes[id];
            return true;
        }

        return false;
    }

    public MeshNode GetNode(string idOrMac) =>
        TryResolve(idOrMac, out MeshNode node)
            ? node
            : throw new KeyNotFoundException($"No node with id or MAC '{idOrMac}'.");

    public IReadOnlyList<MeshEdge> Edges(string nodeId) => _adjacency[nodeId];

    public IEnumerable<MeshEdge> Links() =>
        _adjacency.Values.SelectMany(list => list).Where(e => string.CompareOrdinal(e.From, e.To) < 0);

    public static string NormaliseMac(string mac)
    {
        string hex = new string(mac.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (hex.Length != 12) return mac.Trim().ToUpperInvariant();
        return string.Join(':', Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
    }
}
