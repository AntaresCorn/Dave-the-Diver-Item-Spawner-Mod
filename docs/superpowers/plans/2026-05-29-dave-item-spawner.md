# Dave Item Spawner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and install a BepInEx IL2CPP plugin that opens an F8 in-game item spawner panel for Dave the Diver.

**Architecture:** The plugin is a small net6.0 BepInEx assembly. Pure search/query behavior is kept in testable classes, while game-facing code is isolated behind adapters that call `DataManager`, `DR.Save.SaveSystem`, `SaveData`, and `IngredientsStorage`.

**Tech Stack:** C# net6.0, BepInEx IL2CPP, Unity `OnGUI`, local game interop DLLs, no-package console test harness, `dotnet run`, `dotnet build`.

---

## File Structure

- Create `D:\Code\dave\DaveItemSpawner\DaveItemSpawner.sln`
  - Solution containing plugin and tests.
- Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj`
  - BepInEx plugin project targeting `net6.0`.
- Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\Plugin.cs`
  - BepInEx entry point, config, Unity `Update`, `OnGUI`, and status messages.
- Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\ItemEntry.cs`
  - Small immutable item row model.
- Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\ItemSearch.cs`
  - Pure search/filter/query parsing logic.
- Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\GameItemCatalog.cs`
  - Reads `DataManager.Instance` and builds `ItemEntry` rows.
- Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\GameItemAdder.cs`
  - Adds selected items through game APIs and updates save sections.
- Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\PluginGui.cs`
  - Draws the F8 panel with Unity IMGUI.
- Create `D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests\DaveItemSpawner.Tests.csproj`
  - No-package console test project that exits non-zero on failed assertions.
- Create `D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests\Program.cs`
  - Tests for numeric TID parsing, case-insensitive filtering, and result limiting.

Because `D:\Code\dave` is not a git repository, commit steps are replaced with local status checkpoints.

---

### Task 1: Scaffold Solution And Pure Search Model

**Files:**
- Create: `D:\Code\dave\DaveItemSpawner\DaveItemSpawner.sln`
- Create: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj`
- Create: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\ItemEntry.cs`
- Create: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\ItemSearch.cs`

- [ ] **Step 1: Create folders and solution**

Run:

```powershell
New-Item -ItemType Directory -Force -Path 'D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner'
New-Item -ItemType Directory -Force -Path 'D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests'
dotnet new sln -n DaveItemSpawner -o 'D:\Code\dave\DaveItemSpawner'
```

Expected: solution file created at `D:\Code\dave\DaveItemSpawner\DaveItemSpawner.sln`.

- [ ] **Step 2: Create plugin project file**

Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>DaveItemSpawner</AssemblyName>
    <RootNamespace>DaveItemSpawner</RootNamespace>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="BepInEx.Core" Private="false" HintPath="E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\core\BepInEx.Core.dll" />
    <Reference Include="BepInEx.Unity.IL2CPP" Private="false" HintPath="E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\core\BepInEx.Unity.IL2CPP.dll" />
    <Reference Include="Il2CppInterop.Runtime" Private="false" HintPath="E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\core\Il2CppInterop.Runtime.dll" />
    <Reference Include="Assembly-CSharp" Private="false" HintPath="E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\interop\Assembly-CSharp.dll" />
    <Reference Include="UnityEngine.CoreModule" Private="false" HintPath="E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\interop\UnityEngine.CoreModule.dll" />
    <Reference Include="UnityEngine.IMGUIModule" Private="false" HintPath="E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\interop\UnityEngine.IMGUIModule.dll" />
    <Reference Include="UnityEngine.InputLegacyModule" Private="false" HintPath="E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\interop\UnityEngine.InputLegacyModule.dll" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create item row model**

Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\ItemEntry.cs`:

```csharp
namespace DaveItemSpawner;

public enum ItemAddRoute
{
    Inventory,
    Ingredient
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
```

- [ ] **Step 4: Create search helper**

Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\ItemSearch.cs`:

```csharp
namespace DaveItemSpawner;

public static class ItemSearch
{
    public static bool TryParseTid(string? query, out int tid)
    {
        query = query?.Trim();
        return int.TryParse(query, out tid) && tid > 0;
    }

    public static IReadOnlyList<ItemEntry> Filter(
        IEnumerable<ItemEntry> entries,
        string? query,
        int maxResults)
    {
        var safeMax = Math.Clamp(maxResults, 1, 500);
        var trimmed = query?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            return entries
                .OrderBy(e => e.Tid)
                .Take(safeMax)
                .ToArray();
        }

        if (TryParseTid(trimmed, out var tid))
        {
            return entries
                .Where(e => e.Tid == tid || e.Tid.ToString().Contains(trimmed, StringComparison.Ordinal))
                .OrderBy(e => e.Tid == tid ? 0 : 1)
                .ThenBy(e => e.Tid)
                .Take(safeMax)
                .ToArray();
        }

        return entries
            .Where(e =>
                e.Label.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                e.TextId.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                e.Route.ToString().Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Label.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(e => e.Label)
            .Take(safeMax)
            .ToArray();
    }
}
```

- [ ] **Step 5: Add project to solution and build**

Run:

```powershell
dotnet sln 'D:\Code\dave\DaveItemSpawner\DaveItemSpawner.sln' add 'D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj'
dotnet restore 'D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj'
dotnet build 'D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj'
```

Expected: build succeeds without downloading packages because the project only uses local assembly references.

---

### Task 2: Add Tests For Pure Search Logic

**Files:**
- Create: `D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests\DaveItemSpawner.Tests.csproj`
- Create: `D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests\Program.cs`
- Modify: `D:\Code\dave\DaveItemSpawner\DaveItemSpawner.sln`

- [ ] **Step 1: Create test project**

Create `D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests\DaveItemSpawner.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DaveItemSpawner\DaveItemSpawner.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write failing tests**

Create `D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests\Program.cs`:

```csharp
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
        FilterFindsByCaseInsensitiveName();
        FilterFindsByTextId();
        FilterPrioritizesExactTid();
        FilterHonorsResultLimit();

        Console.WriteLine("All ItemSearch tests passed.");
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

    private static void FilterFindsByCaseInsensitiveName()
    {
        var result = ItemSearch.Filter(Entries, "ore", 10);

        Require(result.Count == 1, "Expected one result for ore.");
        Require(result[0].Tid == 2002, "Expected Copper Ore.");
    }

    private static void FilterFindsByTextId()
    {
        var result = ItemSearch.Filter(Entries, "TRANQ", 10);

        Require(result.Count == 1, "Expected one result for TRANQ.");
        Require(result[0].Tid == 3003, "Expected Tranq Gun.");
    }

    private static void FilterPrioritizesExactTid()
    {
        var result = ItemSearch.Filter(Entries, "1001", 10);

        Require(result.Count > 0, "Expected at least one TID result.");
        Require(result[0].Tid == 1001, "Expected exact TID first.");
    }

    private static void FilterHonorsResultLimit()
    {
        var result = ItemSearch.Filter(Entries, string.Empty, 2);

        Require(result.Count == 2, "Expected result limit of two.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
```

- [ ] **Step 3: Add tests to solution**

Run:

```powershell
dotnet sln 'D:\Code\dave\DaveItemSpawner\DaveItemSpawner.sln' add 'D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests\DaveItemSpawner.Tests.csproj'
```

Expected: solution includes both projects.

- [ ] **Step 4: Run tests and verify behavior**

Run:

```powershell
dotnet run --project 'D:\Code\dave\DaveItemSpawner\tests\DaveItemSpawner.Tests\DaveItemSpawner.Tests.csproj'
```

Expected: console prints `All ItemSearch tests passed.` and exits with code 0.

---

### Task 3: Build Game Catalog Reader

**Files:**
- Create: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\GameItemCatalog.cs`
- Modify: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj`

- [ ] **Step 1: Add Unity Addressables reference if build requests it**

If `DataManager` type usage requires Addressables at compile time, add this reference inside the existing `<ItemGroup>`:

```xml
<Reference Include="Unity.Addressables" Private="false" HintPath="E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\interop\Unity.Addressables.dll" />
```

- [ ] **Step 2: Create catalog reader**

Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\GameItemCatalog.cs`:

```csharp
using BepInEx.Logging;
using Il2CppSystem.Collections.Generic;

namespace DaveItemSpawner;

public sealed class GameItemCatalog
{
    private readonly ManualLogSource _log;
    private readonly List<ItemEntry> _entries = new();
    private bool _loaded;

    public GameItemCatalog(ManualLogSource log)
    {
        _log = log;
    }

    public bool IsLoaded => _loaded;
    public IReadOnlyList<ItemEntry> Entries => _entries;

    public bool TryRefresh()
    {
        if (_loaded)
        {
            return true;
        }

        DataManager? dataManager;
        try
        {
            if (!DataManager.hasInstance)
            {
                return false;
            }

            dataManager = DataManager.Instance;
        }
        catch (Exception ex)
        {
            _log.LogDebug($"DataManager unavailable: {ex.Message}");
            return false;
        }

        if (dataManager is null || !dataManager.IsGameDataLoaded)
        {
            return false;
        }

        var rows = new System.Collections.Generic.Dictionary<int, ItemEntry>();
        AddIntegratedItems(dataManager, rows);
        AddItemBaseRows(dataManager, rows);

        _entries.Clear();
        _entries.AddRange(rows.Values.OrderBy(e => e.Tid));
        _loaded = _entries.Count > 0;

        if (_loaded)
        {
            _log.LogInfo($"Loaded {_entries.Count} item entries.");
        }

        return _loaded;
    }

    public ItemEntry CreateFallback(int tid)
    {
        var route = GuessRouteFromItem(dataManager: null, tid);
        return new ItemEntry(tid, $"TID {tid}", string.Empty, -1, route);
    }

    private void AddIntegratedItems(DataManager dataManager, System.Collections.Generic.Dictionary<int, ItemEntry> rows)
    {
        Dictionary<int, IntegratedItem>? dict;
        try
        {
            dict = dataManager.IntegratedItemDic;
        }
        catch (Exception ex)
        {
            _log.LogDebug($"IntegratedItemDic unavailable: {ex.Message}");
            return;
        }

        if (dict is null)
        {
            return;
        }

        foreach (var pair in dict)
        {
            var item = pair.Value;
            if (item is null)
            {
                continue;
            }

            var tid = item.TID;
            if (tid <= 0 || rows.ContainsKey(tid))
            {
                continue;
            }

            var textId = item.ItemTextID ?? string.Empty;
            var label = BuildLabel(tid, textId, item.SpawnObject ?? string.Empty);
            rows[tid] = new ItemEntry(tid, label, textId, item.IntegratedType, ItemAddRoute.Inventory);
        }
    }

    private void AddItemBaseRows(DataManager dataManager, System.Collections.Generic.Dictionary<int, ItemEntry> rows)
    {
        Dictionary<int, DR.IItemBase>? dict;
        try
        {
            dict = dataManager.ItemBaseDataDic;
        }
        catch (Exception ex)
        {
            _log.LogDebug($"ItemBaseDataDic unavailable: {ex.Message}");
            return;
        }

        if (dict is null)
        {
            return;
        }

        foreach (var pair in dict)
        {
            var tid = pair.Key;
            if (tid <= 0 || rows.ContainsKey(tid))
            {
                continue;
            }

            var route = GuessRouteFromItem(dataManager, tid);
            rows[tid] = new ItemEntry(tid, $"TID {tid}", string.Empty, -1, route);
        }
    }

    public static ItemAddRoute GuessRouteFromItem(DataManager? dataManager, int tid)
    {
        if (dataManager is not null)
        {
            try
            {
                var item = dataManager.GetItems(tid);
                if (item is not null && item.ItemType == (int)ItemType.Ingredient)
                {
                    return ItemAddRoute.Ingredient;
                }

                var ingredientTid = dataManager.GetIngredientTIDFromItemTID(tid);
                if (ingredientTid > 0)
                {
                    return ItemAddRoute.Ingredient;
                }
            }
            catch
            {
                return ItemAddRoute.Inventory;
            }
        }

        return ItemAddRoute.Inventory;
    }

    private static string BuildLabel(int tid, string textId, string spawnObject)
    {
        if (!string.IsNullOrWhiteSpace(textId))
        {
            return textId;
        }

        if (!string.IsNullOrWhiteSpace(spawnObject))
        {
            return spawnObject;
        }

        return $"TID {tid}";
    }
}
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build 'D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj'
```

Expected: build passes or reports exact interop type issues to fix before continuing.

---

### Task 4: Build Game Item Adder

**Files:**
- Create: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\GameItemAdder.cs`

- [ ] **Step 1: Create result model and adder**

Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\GameItemAdder.cs`:

```csharp
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
            var saveData = saveSystem.GetGameSave();
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
        saveSystem.SaveGameData();

        var message = $"Added {amount} x {entry.Tid} to inventory.";
        _log.LogInfo(message);
        return new AddItemResult(true, message);
    }

    private AddItemResult AddIngredient(SaveSystem saveSystem, SaveData saveData, ItemEntry entry, int amount)
    {
        if (!IngredientsStorage.hasInstance)
        {
            return new AddItemResult(false, "Ingredients storage is not ready.");
        }

        var ingredientTid = ResolveIngredientTid(entry.Tid);
        if (ingredientTid <= 0)
        {
            return new AddItemResult(false, $"Could not map TID {entry.Tid} to an ingredient.");
        }

        IngredientsStorage.Instance.AddIngredients(ingredientTid, amount, Place.Main);
        saveData.UpdateIngredientsSave();
        saveSystem.SaveGameData();

        var message = $"Added {amount} x ingredient {ingredientTid}.";
        _log.LogInfo(message);
        return new AddItemResult(true, message);
    }

    private static int ResolveIngredientTid(int tid)
    {
        if (DataManager.hasInstance)
        {
            var dataManager = DataManager.Instance;
            var mapped = dataManager.GetIngredientTIDFromItemTID(tid);
            if (mapped > 0)
            {
                return mapped;
            }
        }

        return tid;
    }
}
```

- [ ] **Step 2: Build**

Run:

```powershell
dotnet build 'D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj'
```

Expected: build passes. If `Place.Main` is ambiguous, fully qualify as `SushiBar.Place.Main`.

---

### Task 5: Build Plugin UI And Entry Point

**Files:**
- Create: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\PluginGui.cs`
- Create: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\Plugin.cs`

- [ ] **Step 1: Create GUI renderer**

Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\PluginGui.cs`:

```csharp
using UnityEngine;

namespace DaveItemSpawner;

public sealed class PluginGui
{
    private readonly GameItemCatalog _catalog;
    private readonly GameItemAdder _adder;
    private readonly int _maxResults;
    private Rect _windowRect = new(60, 60, 680, 520);
    private Vector2 _scroll;
    private string _query = string.Empty;
    private string _amountText = "99";
    private string _status = "Ready.";
    private ItemEntry? _selected;

    public PluginGui(GameItemCatalog catalog, GameItemAdder adder, int maxResults)
    {
        _catalog = catalog;
        _adder = adder;
        _maxResults = maxResults;
    }

    public void Draw()
    {
        _windowRect = GUI.Window(932771, _windowRect, DrawWindow, "Dave Item Spawner");
    }

    private void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        if (!_catalog.TryRefresh())
        {
            GUILayout.Label("Waiting for game data...");
            GUI.DragWindow();
            GUILayout.EndVertical();
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", GUILayout.Width(60));
        _query = GUILayout.TextField(_query, GUILayout.Width(360));
        GUILayout.Label("Amount", GUILayout.Width(60));
        _amountText = GUILayout.TextField(_amountText, GUILayout.Width(80));
        if (GUILayout.Button("Reload", GUILayout.Width(80)))
        {
            _status = $"Catalog has {_catalog.Entries.Count} entries.";
        }
        GUILayout.EndHorizontal();

        var results = ItemSearch.Filter(_catalog.Entries, _query, _maxResults);
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(360));
        foreach (var entry in results)
        {
            var selected = _selected?.Tid == entry.Tid;
            var label = selected ? $"> {entry.Display}" : entry.Display;
            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                _selected = entry;
                _query = entry.Tid.ToString();
                _status = $"Selected {entry.Tid}.";
            }
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Selected", GUILayout.Height(32)))
        {
            AddSelected();
        }
        if (GUILayout.Button("Add TID From Search", GUILayout.Height(32)))
        {
            AddTidFromSearch();
        }
        GUILayout.EndHorizontal();

        GUILayout.Label(_selected is null ? "Selected: none" : $"Selected: {_selected.Display}");
        GUILayout.Label(_status);
        GUILayout.EndVertical();
        GUI.DragWindow();
    }

    private void AddSelected()
    {
        if (_selected is null)
        {
            _status = "Select an item first.";
            return;
        }

        if (!TryGetAmount(out var amount))
        {
            return;
        }

        var result = _adder.Add(_selected, amount);
        _status = result.Message;
    }

    private void AddTidFromSearch()
    {
        if (!ItemSearch.TryParseTid(_query, out var tid))
        {
            _status = "Search box must contain a numeric TID.";
            return;
        }

        if (!TryGetAmount(out var amount))
        {
            return;
        }

        var entry = _catalog.Entries.FirstOrDefault(e => e.Tid == tid) ?? _catalog.CreateFallback(tid);
        _selected = entry;
        var result = _adder.Add(entry, amount);
        _status = result.Message;
    }

    private bool TryGetAmount(out int amount)
    {
        if (!int.TryParse(_amountText.Trim(), out amount) || amount <= 0)
        {
            _status = "Amount must be a positive integer.";
            return false;
        }

        amount = Math.Min(amount, 9999);
        return true;
    }
}
```

- [ ] **Step 2: Create BepInEx entry point**

Create `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\Plugin.cs`:

```csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;

namespace DaveItemSpawner;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "com.local.dave.itemspawner";
    public const string PluginName = "Dave Item Spawner";
    public const string PluginVersion = "0.1.0";

    private ConfigEntry<KeyCode>? _toggleKey;
    private ConfigEntry<int>? _maxResults;
    private PluginGui? _gui;
    private bool _visible;

    public override void Load()
    {
        _toggleKey = Config.Bind("UI", "ToggleKey", KeyCode.F8, "Key used to show or hide the item spawner.");
        _maxResults = Config.Bind("UI", "MaxSearchResults", 100, "Maximum search results shown in the panel.");

        var catalog = new GameItemCatalog(Log);
        var adder = new GameItemAdder(Log);
        _gui = new PluginGui(catalog, adder, Math.Clamp(_maxResults.Value, 10, 500));

        AddComponent<ItemSpawnerBehaviour>().Init(Log, this);
        Log.LogInfo($"{PluginName} loaded. Press {_toggleKey.Value} in game.");
    }

    private void Toggle()
    {
        _visible = !_visible;
    }

    private void Draw()
    {
        if (_visible)
        {
            _gui?.Draw();
        }
    }

    private sealed class ItemSpawnerBehaviour : MonoBehaviour
    {
        private ManualLogSource? _log;
        private Plugin? _plugin;

        public void Init(ManualLogSource log, Plugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        private void Update()
        {
            if (_plugin?._toggleKey is not null && Input.GetKeyDown(_plugin._toggleKey.Value))
            {
                _plugin.Toggle();
            }
        }

        private void OnGUI()
        {
            try
            {
                _plugin?.Draw();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex);
            }
        }
    }
}
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build 'D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\DaveItemSpawner.csproj' -c Release
```

Expected: `DaveItemSpawner.dll` appears under `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\bin\Release\net6.0`.

---

### Task 6: Install Plugin And Verify Loader

**Files:**
- Create directory: `E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\plugins\DaveItemSpawner`
- Copy: `D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\bin\Release\net6.0\DaveItemSpawner.dll`

- [ ] **Step 1: Install the plugin DLL**

Run:

```powershell
New-Item -ItemType Directory -Force -Path 'E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\plugins\DaveItemSpawner'
Copy-Item -Force 'D:\Code\dave\DaveItemSpawner\src\DaveItemSpawner\bin\Release\net6.0\DaveItemSpawner.dll' 'E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\plugins\DaveItemSpawner\DaveItemSpawner.dll'
```

Expected: DLL exists in the plugin folder.

- [ ] **Step 2: Start the game manually**

Run the game from Steam or launch `E:\SteamLibrary\steamapps\common\Dave the Diver\DaveTheDiver.exe`.

Expected: game reaches title screen.

- [ ] **Step 3: Check BepInEx log**

Run:

```powershell
Get-ChildItem -Path 'E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx' -Filter 'LogOutput.log' -Recurse
Get-Content -Path 'E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\LogOutput.log' -Tail 120
```

Expected log contains `Dave Item Spawner loaded`. No plugin load exception appears.

---

### Task 7: In-Game Smoke Test

**Files:**
- Read: `E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\LogOutput.log`
- Modify only by game/plugin runtime: active save data

- [ ] **Step 1: Toggle UI**

Press `F8` after entering a save.

Expected: `Dave Item Spawner` window appears. Pressing `F8` again hides it.

- [ ] **Step 2: Test direct TID path**

Enter a known low-risk numeric TID in Search, set Amount to `1`, and click `Add TID From Search`.

Expected: panel reports either a success message or a controlled failure without crashing.

- [ ] **Step 3: Test catalog search**

Search for a known text fragment from item ids, select a result, set Amount to `1`, and click `Add Selected`.

Expected: selected item is routed through inventory or ingredient storage. Failure is shown in-panel and logged.

- [ ] **Step 4: Review log**

Run:

```powershell
Get-Content -Path 'E:\SteamLibrary\steamapps\common\Dave the Diver\BepInEx\LogOutput.log' -Tail 200
```

Expected: successes are logged with item counts. Failures include exception details and do not crash the game.

---

## Plan Self-Review

- Spec coverage: F8 panel, search, TID entry, amount input, inventory route, ingredient route, save update, logging, build, install, rollback all have tasks.
- Red-flag scan: no forbidden deferred-work markers are present.
- Type consistency: all plugin files use `ItemEntry`, `ItemAddRoute`, `GameItemCatalog`, `GameItemAdder`, and `PluginGui` consistently.
- Known risk: item display names may initially show text IDs instead of localized names. The design allows this for the first implementation because numeric TID and text ID search still work.
