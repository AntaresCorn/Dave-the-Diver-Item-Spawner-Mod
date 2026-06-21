namespace DaveItemSpawner;

public static class ItemRouting
{
    public static ItemAddRoute ResolveRoute(int itemType, int ingredientTid)
    {
        if (itemType == (int)ItemType.Ingredient ||
            itemType == (int)ItemType.Ingredient_Catch ||
            ingredientTid > 0)
        {
            return ItemAddRoute.Ingredient;
        }

        return ItemAddRoute.Inventory;
    }
}
