namespace SmartX.PoE.Api.DataStructures;

public sealed class RedBlackTree<TKey, TValue> where TKey : IComparable<TKey>
{
    private const bool Red = true;
    private const bool Black = false;

    private sealed class Node
    {
        public TKey Key;
        public TValue Value;
        public Node? Left;
        public Node? Right;
        public bool Color; 

        public Node(TKey key, TValue value, bool color)
        {
            Key = key;
            Value = value;
            Color = color;
        }
    }

    private Node? _root;

    public int Count { get; private set; }

    public bool IsEmpty => _root is null;

    public void Insert(TKey key, TValue value)
    {
        _root = Insert(_root, key, value);
        _root.Color = Black;
    }

    private Node Insert(Node? h, TKey key, TValue value)
    {
        if (h is null)
        {
            Count++;
            return new Node(key, value, Red);
        }

        int cmp = key.CompareTo(h.Key);
        if (cmp < 0) h.Left = Insert(h.Left, key, value);
        else if (cmp > 0) h.Right = Insert(h.Right, key, value);
        else h.Value = value;

        if (IsRed(h.Right) && !IsRed(h.Left)) h = RotateLeft(h);
        if (IsRed(h.Left) && IsRed(h.Left!.Left)) h = RotateRight(h);
        if (IsRed(h.Left) && IsRed(h.Right)) FlipColors(h);
        return h;
    }


    public bool TryGetValue(TKey key, out TValue? value)
    {
        Node? x = _root;
        while (x is not null)
        {
            int cmp = key.CompareTo(x.Key);
            if (cmp < 0) x = x.Left;
            else if (cmp > 0) x = x.Right;
            else
            {
                value = x.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public bool TryGetMin(out TKey? key, out TValue? value)
    {
        if (_root is null)
        {
            key = default;
            value = default;
            return false;
        }

        Node x = _root;
        while (x.Left is not null) x = x.Left;
        key = x.Key;
        value = x.Value;
        return true;
    }

    public bool TryGetMax(out TKey? key, out TValue? value)
    {
        if (_root is null)
        {
            key = default;
            value = default;
            return false;
        }

        Node x = _root;
        while (x.Right is not null) x = x.Right;
        key = x.Key;
        value = x.Value;
        return true;
    }

    public List<TValue> Range(TKey low, TKey high)
    {
        var results = new List<TValue>();
        if (low.CompareTo(high) > 0) return results;
        Range(_root, low, high, results);
        return results;
    }

    private static void Range(Node? x, TKey low, TKey high, List<TValue> results)
    {
        if (x is null) return;

        int cmpLow = low.CompareTo(x.Key);
        int cmpHigh = high.CompareTo(x.Key);

        if (cmpLow < 0) Range(x.Left, low, high, results);              
        if (cmpLow <= 0 && cmpHigh >= 0) results.Add(x.Value);          
        if (cmpHigh > 0) Range(x.Right, low, high, results);             
    }

    public List<TValue> InOrder()
    {
        var results = new List<TValue>(Count);
        InOrder(_root, results);
        return results;
    }

    private static void InOrder(Node? x, List<TValue> results)
    {
        if (x is null) return;
        InOrder(x.Left, results);
        results.Add(x.Value);
        InOrder(x.Right, results);
    }

    public int Height() => Height(_root);

    private static int Height(Node? x) => x is null ? 0 : 1 + Math.Max(Height(x.Left), Height(x.Right));

    public bool IsValid()
    {
        if (_root is null) return true;
        if (IsRed(_root)) return false;
        return IsOrdered(_root, default, false, default, false) && BlackHeight(_root) >= 0;
    }

    private static bool IsOrdered(Node? x, TKey? min, bool hasMin, TKey? max, bool hasMax)
    {
        if (x is null) return true;
        if (hasMin && x.Key.CompareTo(min!) <= 0) return false;
        if (hasMax && x.Key.CompareTo(max!) >= 0) return false;
        return IsOrdered(x.Left, min, hasMin, x.Key, true) && IsOrdered(x.Right, x.Key, true, max, hasMax);
    }


    private static int BlackHeight(Node? x)
    {
        if (x is null) return 0;
        if (IsRed(x.Right)) return -1;                    
        if (IsRed(x) && IsRed(x.Left)) return -1;           

        int left = BlackHeight(x.Left);
        int right = BlackHeight(x.Right);
        if (left < 0 || right < 0 || left != right) return -1;
        return left + (IsRed(x) ? 0 : 1);
    }


    private static bool IsRed(Node? x) => x is not null && x.Color == Red;

    private static Node RotateLeft(Node h)
    {
        Node x = h.Right!;
        h.Right = x.Left;
        x.Left = h;
        x.Color = h.Color;
        h.Color = Red;
        return x;
    }

    private static Node RotateRight(Node h)
    {
        Node x = h.Left!;
        h.Left = x.Right;
        x.Right = h;
        x.Color = h.Color;
        h.Color = Red;
        return x;
    }

    private static void FlipColors(Node h)
    {
        h.Color = Red;
        h.Left!.Color = Black;
        h.Right!.Color = Black;
    }
}
