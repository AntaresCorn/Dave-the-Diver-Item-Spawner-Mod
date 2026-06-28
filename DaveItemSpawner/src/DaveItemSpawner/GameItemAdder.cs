using BepInEx.Logging;
using SushiBar;

namespace DaveItemSpawner;

public sealed record AddItemResult(bool Success, string Message);

public sealed class GameItemAdder
{
    private readonly ManualLogSource _log;

    public GameItemAdder(ManualLogSource log)
    {
        _log = log;
    }

    public AddItemResult Add(ItemEntry entry, int amount)
    {
        if (amount <= 0)
        {
            return new AddItemResult(false, "Amount must be greater than zero.");
        }

        try
        {
            if (!InteropAccess.TryGetSaveSystem(out var saveSystem) || saveSystem is null)
            {
                return new AddItemResult(false, "Save system is not ready.");
            }

            var saveData = InteropAccess.GetGameSave();
            if (saveData is null)
            {
                return new AddItemResult(false, "Game save is not loaded.");
            }

            var route = ResolveRouteForCurrentScene(entry);
            _log.LogInfo($"Routing TID {entry.Tid} to {route} in scene '{InteropAccess.GetActiveSceneName()}'.");

            return route switch
            {
                ItemAddRoute.Ingredient => AddIngredient(saveSystem, saveData, entry, amount),
                ItemAddRoute.JungleInventory => AddJungleInventoryItem(saveSystem, saveData, entry, amount),
                _ => AddInventoryItem(saveSystem, saveData, entry, amount),
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex);
            return new AddItemResult(false, $"Failed: {ex.Message}");
        }
    }

    private static ItemAddRoute ResolveRouteForCurrentScene(ItemEntry entry)
    {
        // Jungle scenes always target jungle inventory, regardless of catalog guess.
        if (InteropAccess.IsJungleSceneActive())
        {
            return ItemAddRoute.JungleInventory;
        }

        return entry.Route;
    }

    private AddItemResult AddInventoryItem(object saveSystem, SaveData saveData, ItemEntry entry, int amount)
    {
        // Apply mutation then flush save data so the item persists after scene reload.
        InteropAccess.AddInventoryItemSaveData(saveData, entry.Tid, amount);
        InteropAccess.UpdateInventoryItemSave(saveData);

        if (!InteropAccess.SaveGameData(saveSystem))
        {
            return new AddItemResult(false, $"Failed to save inventory after adding {amount} x {Describe(entry)}.");
        }

        var message = $"Added {amount} x {Describe(entry)} to inventory.";
        _log.LogInfo(message);
        return new AddItemResult(true, message);
    }

    private AddItemResult AddJungleInventoryItem(object saveSystem, SaveData saveData, ItemEntry entry, int amount)
    {
        var jungleSave = InteropAccess.GetJungleSave(saveData);
        if (jungleSave is null)
        {
            return new AddItemResult(false, "Jungle save data is not ready.");
        }

        InteropAccess.AddJungleVilItemSaveData(jungleSave, entry.Tid, amount);
        InteropAccess.UpdateJungleVilItemSave(jungleSave);
        InteropAccess.UpdateJungleSave(jungleSave);

        if (!InteropAccess.SaveGameData(saveSystem))
        {
            return new AddItemResult(false, $"Failed to save jungle inventory after adding {amount} x {Describe(entry)}.");
        }

        var message = $"Added {amount} x {Describe(entry)} to jungle inventory.";
        _log.LogInfo(message);
        return new AddItemResult(true, message);
    }

    private AddItemResult AddIngredient(object saveSystem, SaveData saveData, ItemEntry entry, int amount)
    {
        if (!InteropAccess.TryGetIngredientsStorage(out var ingredientsStorage) || ingredientsStorage is null)
        {
            return new AddItemResult(false, "Ingredients storage is not ready.");
        }

        var ingredientTid = ResolveIngredientTid(entry);
        if (ingredientTid <= 0)
        {
            return new AddItemResult(false, $"Could not map TID {entry.Tid} to an ingredient.");
        }

        InteropAccess.AddIngredients(ingredientsStorage, ingredientTid, amount, Place.Main);
        InteropAccess.UpdateIngredientsSave(saveData);

        if (!InteropAccess.SaveGameData(saveSystem))
        {
            return new AddItemResult(false, $"Failed to save ingredients after adding {amount} x {ingredientTid}.");
        }

        var message = $"Added {amount} x ingredient {ingredientTid}.";
        _log.LogInfo(message);
        return new AddItemResult(true, message);
    }

    private static int ResolveIngredientTid(ItemEntry entry)
    {
        // Some items map to a distinct ingredient id through DataManager.
        var mapped = InteropAccess.GetIngredientTidFromItemTid(entry.Tid);
        if (mapped > 0)
        {
            return mapped;
        }

        if (entry.ItemType == (int)ItemType.Ingredient ||
            entry.ItemType == (int)ItemType.Ingredient_Catch)
        {
            return entry.Tid;
        }

        return 0;
    }

    private static string Describe(ItemEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Label))
        {
            return entry.Label;
        }

        return $"TID {entry.Tid}";
    }
}
