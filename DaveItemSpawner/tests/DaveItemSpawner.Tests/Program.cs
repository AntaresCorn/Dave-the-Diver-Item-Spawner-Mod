using DaveItemSpawner;

namespace DaveItemSpawner.Tests;

public static class Program
{
    private static readonly ItemEntry[] Entries =
    [
        new(1001, "Salt", "ITEM_SALT", 4, ItemAddRoute.Ingredient),
        new(2002, "Copper Ore", "ITEM_COPPER_ORE", 2, ItemAddRoute.Inventory),
        new(3003, "Tranq Gun", "ITEM_TRANQ_GUN", 0, ItemAddRoute.Inventory),
    ];

    public static int Main()
    {
        TryParseTidAcceptsPositiveNumbers("1001", 1001);
        TryParseTidAcceptsPositiveNumbers(" 2002 ", 2002);
        TryParseTidRejectsInvalidValues("");
        TryParseTidRejectsInvalidValues("abc");
        TryParseTidRejectsInvalidValues("-1");
        TryParseTidRejectsInvalidValues("0");
        TryParseAmountAcceptsPositiveNumbers(" 7 ", 7);
        TryParseAmountRejectsInvalidValues("");
        TryParseAmountRejectsInvalidValues("abc");
        TryParseAmountRejectsInvalidValues("0");
        TryParseAmountRejectsInvalidValues("-1");
        TryParseAmountClampsLargeValues("10000", 9999);
        FilterFindsByCaseInsensitiveName();
        FilterFindsByTextId();
        FilterPrioritizesExactTid();
        FilterHonorsResultLimit();
        FilterClampsZeroResultLimitToOne();
        FilterClampsOversizedResultLimitToFiveHundred();
        FilterToleratesNullLabelAndTextId();
        EmptyQueryReturnsEntriesOrderedByTid();
        ResolveRouteTreatsIngredientTypeAsIngredient();
        ResolveRouteTreatsIngredientMappingAsIngredient();
        ResolveRouteTreatsOtherItemsAsInventory();

        Console.WriteLine("All item helper tests passed.");
        return 0;
    }

    private static void TryParseTidAcceptsPositiveNumbers(string query, int expected)
    {
        Require(ItemSearch.TryParseTid(query, out var tid), $"Expected '{query}' to parse.");
        Require(tid == expected, $"Expected {expected}, got {tid}.");
    }

    private static void TryParseTidRejectsInvalidValues(string query)
    {
        Require(!ItemSearch.TryParseTid(query, out _), $"Expected '{query}' to be rejected.");
    }

    private static void TryParseAmountAcceptsPositiveNumbers(string query, int expected)
    {
        Require(ItemInput.TryParseAmount(query, out var amount), $"Expected '{query}' to parse.");
        Require(amount == expected, $"Expected {expected}, got {amount}.");
    }

    private static void TryParseAmountRejectsInvalidValues(string query)
    {
        Require(!ItemInput.TryParseAmount(query, out _), $"Expected '{query}' to be rejected.");
    }

    private static void TryParseAmountClampsLargeValues(string query, int expected)
    {
        Require(ItemInput.TryParseAmount(query, out var amount), $"Expected '{query}' to parse.");
        Require(amount == expected, $"Expected clamped amount {expected}, got {amount}.");
    }

    private static void FilterFindsByCaseInsensitiveName()
    {
        var result = ItemSearch.Filter(Entries, "ore", 10);

        Require(result.Count == 1, "Expected one result for ore.");
        Require(result[0].Tid == 2002, "Expected Copper Ore.");
    }

    private static void FilterFindsByTextId()
    {
        var result = ItemSearch.Filter(Entries, "COPPER_ORE", 10);

        Require(result.Count == 1, "Expected one result for COPPER_ORE.");
        Require(result[0].Tid == 2002, "Expected Copper Ore.");
    }

    private static void FilterPrioritizesExactTid()
    {
        var entries = new[]
        {
            new ItemEntry(10010, "Salt Pack", "ITEM_SALT_PACK", 4, ItemAddRoute.Ingredient),
            new ItemEntry(1001, "Salt", "ITEM_SALT", 4, ItemAddRoute.Ingredient),
        };

        var result = ItemSearch.Filter(entries, "1001", 10);

        Require(result.Count == 2, "Expected exact and partial TID results.");
        Require(result[0].Tid == 1001, "Expected exact TID first.");
        Require(result[1].Tid == 10010, "Expected partial TID second.");
    }

    private static void FilterHonorsResultLimit()
    {
        var result = ItemSearch.Filter(Entries, string.Empty, 2);

        Require(result.Count == 2, "Expected result limit of two.");
    }

    private static void FilterClampsZeroResultLimitToOne()
    {
        var result = ItemSearch.Filter(Entries, string.Empty, 0);

        Require(result.Count == 1, "Expected zero result limit to clamp to one.");
    }

    private static void FilterClampsOversizedResultLimitToFiveHundred()
    {
        var entries = Enumerable.Range(1, 501)
            .Select(tid => new ItemEntry(tid, $"Item {tid}", $"ITEM_{tid}", 0, ItemAddRoute.Inventory))
            .ToArray();

        var result = ItemSearch.Filter(entries, string.Empty, 1_000);

        Require(result.Count == 500, "Expected oversized result limit to clamp to 500.");
    }

    private static void FilterToleratesNullLabelAndTextId()
    {
        var entries = new[]
        {
            new ItemEntry(4004, null!, null!, 0, ItemAddRoute.Inventory),
        };

        var result = ItemSearch.Filter(entries, "missing", 10);

        Require(result.Count == 0, "Expected null-ish label and text id to be searchable without throwing.");
    }

    private static void EmptyQueryReturnsEntriesOrderedByTid()
    {
        var entries = new[]
        {
            new ItemEntry(3003, "Tranq Gun", "ITEM_TRANQ_GUN", 0, ItemAddRoute.Inventory),
            new ItemEntry(1001, "Salt", "ITEM_SALT", 4, ItemAddRoute.Ingredient),
            new ItemEntry(2002, "Copper Ore", "ITEM_COPPER_ORE", 2, ItemAddRoute.Inventory),
        };

        var result = ItemSearch.Filter(entries, string.Empty, 10);

        Require(result.Count == 3, "Expected all entries for empty query.");
        Require(result[0].Tid == 1001, "Expected first empty-query result to have lowest TID.");
        Require(result[1].Tid == 2002, "Expected second empty-query result to have middle TID.");
        Require(result[2].Tid == 3003, "Expected third empty-query result to have highest TID.");
    }

    private static void ResolveRouteTreatsIngredientTypeAsIngredient()
    {
        Require(ItemRouting.ResolveRoute(4, 0) == ItemAddRoute.Ingredient,
            "Expected ingredient item type to route to ingredients.");
    }

    private static void ResolveRouteTreatsIngredientMappingAsIngredient()
    {
        Require(ItemRouting.ResolveRoute(0, 1234) == ItemAddRoute.Ingredient,
            "Expected ingredient mapping to route to ingredients.");
    }

    private static void ResolveRouteTreatsOtherItemsAsInventory()
    {
        Require(ItemRouting.ResolveRoute(0, 0) == ItemAddRoute.Inventory,
            "Expected ordinary items to route to inventory.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
