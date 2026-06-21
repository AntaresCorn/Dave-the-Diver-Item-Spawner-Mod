# Dave the Diver Item Spawner Design

Date: 2026-05-29

## Goal

Build a local BepInEx IL2CPP plugin for Dave the Diver that opens an in-game item spawner panel. The panel lets the user search for items, choose a quantity, and add items through the game's own save/storage APIs.

## Target Environment

- Game path: `E:\SteamLibrary\steamapps\common\Dave the Diver`
- Runtime: Unity IL2CPP
- Mod loader: BepInEx IL2CPP is already installed
- Existing interop assemblies are available in `BepInEx\interop`
- Build machine has .NET SDK installed

## User Experience

- Press `F8` to open or close the item spawner panel.
- Search by item name text, internal text id, or numeric `TID`.
- Select a search result from a scrollable list.
- Enter an amount and click Add.
- Show a short status message in the panel after each add attempt.
- Log successes and failures through the BepInEx logger.

## Scope

First implementation:

- Create a new BepInEx plugin DLL named `DaveItemSpawner.dll`.
- Render a simple Unity `OnGUI` panel.
- Build a searchable item index after game data has loaded.
- Support direct `TID` entry even if display text lookup is incomplete.
- Add normal items with `SaveData.AddInventoryItemSaveData`.
- Add ingredients through `IngredientsStorage.AddIngredients`.
- Trigger save updates using the game's save APIs after mutation.

Out of scope for the first implementation:

- Rich custom UI prefabs.
- Editing arbitrary save fields.
- Unlocking story progression, DLC flags, staff, recipes, or achievements.
- Bypassing all mission/item restrictions.
- External memory patching.

## Architecture

The plugin will have three small responsibilities:

- `Plugin`: BepInEx entry point, config binding, logging, and Unity message forwarding.
- `ItemCatalog`: reads `DataManager.Instance` and builds searchable `ItemEntry` rows from available item data.
- `ItemAdder`: routes additions to the safest known game API based on item type.

The UI remains thin. It asks the catalog for filtered rows, sends the selected entry and amount to the adder, then displays the result.

## Data Flow

1. Plugin loads with BepInEx.
2. On each update, `F8` toggles the panel.
3. When the panel opens, the catalog checks whether `DataManager.Instance` is ready.
4. The catalog reads integrated item/item dictionaries if available, and creates rows with `TID`, display label, and item type.
5. User selects an item and amount.
6. `ItemAdder` gets `SaveData` from `DR.Save.SaveSystem.Instance.GetGameSave()`.
7. If the item is an ingredient or maps to an ingredient id, add it to `IngredientsStorage`.
8. Otherwise, add it to inventory with `SaveData.AddInventoryItemSaveData`.
9. Update the relevant save section and report success or failure.

## Error Handling

- If game data is not loaded, the panel shows a waiting message.
- If save data is unavailable, the Add button returns a clear failure.
- If a game API call throws, catch it, show a concise failure message, and log the exception.
- If a search returns no results, still allow direct numeric `TID` addition.
- Special items may fail or behave differently; the plugin will report the failure and will not write to unverified save structures.

## Testing

Local verification will include:

- Build succeeds without downloading packages.
- DLL is copied into `BepInEx\plugins\DaveItemSpawner`.
- Game starts without BepInEx loader errors.
- F8 opens and closes the panel.
- Searching by numeric `TID` works.
- Adding a low-risk ingredient increases the ingredient count.
- Adding a low-risk normal item appears in inventory or logs a controlled failure.

## Rollback

The mod is installed as a standalone plugin folder. Removing `BepInEx\plugins\DaveItemSpawner` disables it. No base game files need to be overwritten.
