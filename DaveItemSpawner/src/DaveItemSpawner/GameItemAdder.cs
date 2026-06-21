using BepInEx.Logging;
using DR.Save;
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
            if (!SaveSystem.hasInstance)
            {
                return new AddItemResult(false, "Save system is not ready.");
            }

            var saveSystem = SaveSystem.Instance;
            var saveData = SaveSystem.GetGameSave();
            if (saveData is null)
            {
                return new AddItemResult(false, "Game save is not loaded.");
            }

            return entry.Route == ItemAddRoute.Ingredient
                ? AddIngredient(saveSystem, saveData, entry, amount)
                : AddInventoryItem(saveSystem, saveData, entry, amount);
        }
        catch (Exception ex)
        {
            _log.LogError(ex);
            return new AddItemResult(false, $"Failed: {ex.Message}");
        }
    }

    private AddItemResult AddInventoryItem(SaveSystem saveSystem, SaveData saveData, ItemEntry entry, int amount)
    {
        saveData.AddInventoryItemSaveData(entry.Tid, amount, null);
        saveData.UpdateInventoryItemSave();

        if (!saveSystem.SaveGameData())
        {
            return new AddItemResult(false, $"Failed to save inventory after adding {amount} x {Describe(entry)}.");
        }

        var message = $"Added {amount} x {Describe(entry)} to inventory.";
        _log.LogInfo(message);
        return new AddItemResult(true, message);
    }

    private AddItemResult AddIngredient(SaveSystem saveSystem, SaveData saveData, ItemEntry entry, int amount)
    {
        if (!IngredientsStorage.hasInstance)
        {
            return new AddItemResult(false, "Ingredients storage is not ready.");
        }

        var ingredientTid = ResolveIngredientTid(entry);
        if (ingredientTid <= 0)
        {
            return new AddItemResult(false, $"Could not map TID {entry.Tid} to an ingredient.");
        }

        IngredientsStorage.Instance.AddIngredients(ingredientTid, amount, Place.Main);
        saveData.UpdateIngredientsSave();

        if (!saveSystem.SaveGameData())
        {
            return new AddItemResult(false, $"Failed to save ingredients after adding {amount} x {ingredientTid}.");
        }

        var message = $"Added {amount} x ingredient {ingredientTid}.";
        _log.LogInfo(message);
        return new AddItemResult(true, message);
    }

    private static int ResolveIngredientTid(ItemEntry entry)
    {
        if (DataManager.hasInstance)
        {
            var mapped = DataManager.GetIngredientTIDFromItemTID(entry.Tid);
            if (mapped > 0)
            {
                return mapped;
            }
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
