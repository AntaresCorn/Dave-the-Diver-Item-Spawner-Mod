namespace DaveItemSpawner;

public enum ItemAddRoute
{
    Inventory,
    Ingredient,
    JungleInventory
}

public sealed record ItemEntry(
    int Tid,
    string Label,
    string TextId,
    int ItemType,
    ItemAddRoute Route)
{
    public string Display => $"{Tid} | {Label} | {TextId} | {Route}";
}
