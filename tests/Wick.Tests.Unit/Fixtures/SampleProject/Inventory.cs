namespace SampleProject;

/// <summary>
/// Fixture type with well-defined members for Sub-spec D analysis tool tests.
/// Provides a predictable set of methods, properties, fields, and an event
/// for asserting against csharp_find_symbol and csharp_get_member_signatures.
/// </summary>
public class Inventory
{
    private readonly List<string> _items = [];

    public int Capacity { get; set; } = 20;

    public int Count => _items.Count;

    public event EventHandler? ItemAdded;

    public void AddItem(string itemName)
    {
        if (_items.Count >= Capacity)
        {
            return;
        }

        _items.Add(itemName);
        ItemAdded?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveItem(string itemName)
    {
        return _items.Remove(itemName);
    }

    public bool HasItem(string itemName)
    {
        return _items.Contains(itemName);
    }

    internal void Clear()
    {
        _items.Clear();
    }

    private static int MaxStackSize => 99;
}
