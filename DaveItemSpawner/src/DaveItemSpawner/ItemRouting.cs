namespace DaveItemSpawner;

public static class ItemRouting
{
    // Chooses which save subsystem should receive the item.
    public static ItemAddRoute ResolveRoute(int itemType, int ingredientTid)
    {
        if (IsJungleItemType(itemType))
        {
            return ItemAddRoute.JungleInventory;
        }

        if (itemType == (int)ItemType.Ingredient ||
            itemType == (int)ItemType.Ingredient_Catch ||
            ingredientTid > 0)
        {
            return ItemAddRoute.Ingredient;
        }

        return ItemAddRoute.Inventory;
    }

    private static bool IsJungleItemType(int itemType)
    {
        return itemType == (int)ItemType.JungleMissionItem ||
            itemType == (int)ItemType.JungleCommon ||
            itemType == (int)ItemType.DogFood ||
            itemType == (int)ItemType.JungleMedicine ||
            itemType == (int)ItemType.JungleInsect ||
            itemType == (int)ItemType.JungleFishingRod ||
            itemType == (int)ItemType.JungleFishingBait ||
            itemType == (int)ItemType.JungleFishingRepairKit ||
            itemType == (int)ItemType.HousingFurniture ||
            itemType == (int)ItemType.HousingFurnitureBed ||
            itemType == (int)ItemType.HousingSpecial ||
            itemType == (int)ItemType.HousingFurnitureProp ||
            itemType == (int)ItemType.HousingCDPlayer ||
            itemType == (int)ItemType.HousingExterior;
    }
}
