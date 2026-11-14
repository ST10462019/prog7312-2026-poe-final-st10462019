namespace SmartX.PoE.Api.DataStructures;

public sealed class MinHeap<T>
{
    private readonly List<T> _items = new();
    private readonly IComparer<T> _comparer;

    public MinHeap(IComparer<T>? comparer = null)
    {
        _comparer = comparer ?? Comparer<T>.Default;
    }

    public int Count => _items.Count;

    public bool IsEmpty => _items.Count == 0;

    public T Peek()
    {
        if (_items.Count == 0) throw new InvalidOperationException("The heap is empty.");
        return _items[0];
    }

    public void Push(T item)
    {
        _items.Add(item);
        SiftUp(_items.Count - 1);
    }

    public T Pop()
    {
        if (_items.Count == 0) throw new InvalidOperationException("The heap is empty.");

        T root = _items[0];
        T last = _items[^1];
        _items.RemoveAt(_items.Count - 1);

        if (_items.Count > 0)
        {
            _items[0] = last;
            SiftDown(0);
        }

        return root;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_comparer.Compare(_items[index], _items[parent]) >= 0) break;
            Swap(index, parent);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        int count = _items.Count;
        while (true)
        {
            int left = 2 * index + 1;
            int right = left + 1;
            int smallest = index;

            if (left < count && _comparer.Compare(_items[left], _items[smallest]) < 0) smallest = left;
            if (right < count && _comparer.Compare(_items[right], _items[smallest]) < 0) smallest = right;
            if (smallest == index) return;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int a, int b) => (_items[a], _items[b]) = (_items[b], _items[a]);
}
