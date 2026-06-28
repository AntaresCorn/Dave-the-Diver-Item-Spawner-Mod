using BepInEx.Logging;

namespace DaveItemSpawner;

public sealed class GameItemCatalog
{
    private readonly ManualLogSource _log;
    private readonly System.Collections.Generic.List<ItemEntry> _entries = new();
    private bool _loaded;

    public GameItemCatalog(ManualLogSource log)
    {
        _log = log;
    }

    public bool IsLoaded => _loaded;

    public System.Collections.Generic.IReadOnlyList<ItemEntry> Entries => _entries;

    // Builds the in-memory item catalog once the game's data manager is ready.
    public bool TryRefresh()
    {
        if (_loaded)
        {
            return true;
        }

        object? dataManager;
        try
        {
            if (!InteropAccess.TryGetDataManager(out dataManager))
            {
                _log.LogDebug("DataManager unavailable: no instance.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log.LogDebug($"DataManager unavailable: {ex.Message}");
            return false;
        }

        if (dataManager is null || !InteropAccess.IsGameDataLoaded(dataManager))
        {
            _log.LogDebug("DataManager unavailable: game data is not loaded.");
            return false;
        }

        var rows = new System.Collections.Generic.Dictionary<int, ItemEntry>();
        // Merge both sources; integrated rows usually carry richer metadata.
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
        // Used when a TID is valid for add operations but absent from current catalog snapshot.
        var route = InteropAccess.TryGetDataManager(out var dataManager)
            ? GuessRouteFromItem(dataManager, tid)
            : ItemAddRoute.Inventory;
        return new ItemEntry(tid, $"TID {tid}", string.Empty, -1, route);
    }

    private void AddIntegratedItems(object dataManager, System.Collections.Generic.Dictionary<int, ItemEntry> rows)
    {
        object? dict;
        try
        {
            dict = InteropAccess.GetIntegratedItemDictionary(dataManager);
        }
        catch (Exception ex)
        {
            LogDebugException("IntegratedItemDic unavailable", ex);
            return;
        }

        if (dict is null)
        {
            return;
        }

        try
        {
            foreach (var value in EnumerateMember(dict, "Values"))
            {
                if (value is null)
                {
                    continue;
                }

                var tid = InteropAccess.GetIntProperty(value, "TID");
                if (tid <= 0 || rows.ContainsKey(tid))
                {
                    continue;
                }

                var textId = InteropAccess.GetStringProperty(value, "ItemTextID") ?? string.Empty;
                var spawnObject = InteropAccess.GetStringProperty(value, "SpawnObject") ?? string.Empty;
                var label = BuildLabel(dataManager, tid, textId, spawnObject);
                var route = GuessRouteFromItem(dataManager, tid);
                var itemType = InteropAccess.GetIntProperty(value, "IntegratedType");
                rows[tid] = new ItemEntry(tid, label, textId, itemType, route);
            }
        }
        catch (Exception ex)
        {
            LogDebugException("IntegratedItemDic enumeration failed", ex);
        }
    }

    private void AddItemBaseRows(object dataManager, System.Collections.Generic.Dictionary<int, ItemEntry> rows)
    {
        object? dict;
        try
        {
            dict = InteropAccess.GetItemBaseDataDictionary(dataManager);
        }
        catch (Exception ex)
        {
            LogDebugException("ItemBaseDataDic unavailable", ex);
            return;
        }

        if (dict is null)
        {
            return;
        }

        try
        {
            foreach (var value in EnumerateMember(dict, "Values"))
            {
                if (value is null)
                {
                    continue;
                }

                var tid = InteropAccess.GetIntProperty(value, "TID");
                if (tid <= 0 || rows.ContainsKey(tid))
                {
                    continue;
                }

                var textId = InteropAccess.GetStringProperty(value, "ItemTextID") ?? string.Empty;
                var label = BuildLabel(dataManager, tid, textId, string.Empty);
                var route = GuessRouteFromItem(dataManager, tid);
                var itemType = InteropAccess.GetIntProperty(value, "ItemType");
                rows[tid] = new ItemEntry(tid, label, textId, itemType, route);
            }
        }
        catch (Exception ex)
        {
            LogDebugException("ItemBaseDataDic enumeration failed", ex);
        }
    }

    public ItemAddRoute GuessRouteFromItem(object? dataManager, int tid)
    {
        if (dataManager is not null)
        {
            try
            {
                // Route depends on both item type and ingredient remap table.
                var item = InteropAccess.GetItemByTid(dataManager, tid);
                var itemType = item is null ? 0 : InteropAccess.GetIntProperty(item, "ItemType");
                var ingredientTid = InteropAccess.GetIngredientTidFromItemTid(tid);
                return ItemRouting.ResolveRoute(itemType, ingredientTid);
            }
            catch (Exception ex)
            {
                LogDebugException($"Route detection failed for TID {tid}", ex);
                return ItemAddRoute.Inventory;
            }
        }

        return ItemAddRoute.Inventory;
    }

    private static string BuildLabel(object dataManager, int tid, string textId, string spawnObject)
    {
        if (!string.IsNullOrWhiteSpace(textId))
        {
            try
            {
                // Prefer localized text so users can search by in-game display names.
                var localized = InteropAccess.GetLocalizedText(dataManager, textId);
                if (!string.IsNullOrWhiteSpace(localized) &&
                    !string.Equals(localized, textId, StringComparison.Ordinal))
                {
                    return localized;
                }
            }
            catch
            {
                // Fall back to the raw text id below.
            }

            return textId;
        }

        if (!string.IsNullOrWhiteSpace(spawnObject))
        {
            return spawnObject;
        }

        return $"TID {tid}";
    }

    private void LogDebugException(string source, Exception ex)
    {
        _log.LogDebug($"{source}: {ex.GetType().Name}: {ex.Message}");
    }

    private static System.Collections.Generic.IEnumerable<object?> EnumerateMember(object source, string memberName)
    {
        var member = source.GetType().GetProperty(memberName)?.GetValue(source);
        if (member is null)
        {
            yield break;
        }

        var enumerator = member.GetType().GetMethod("GetEnumerator", Type.EmptyTypes)?.Invoke(member, null);
        if (enumerator is null)
        {
            yield break;
        }

        var enumeratorType = enumerator.GetType();
        var moveNext = enumeratorType.GetMethod("MoveNext", Type.EmptyTypes);
        var current = enumeratorType.GetProperty("Current");
        if (moveNext is null || current is null)
        {
            yield break;
        }

        try
        {
            while (moveNext.Invoke(enumerator, null) is true)
            {
                yield return current.GetValue(enumerator);
            }
        }
        finally
        {
            if (enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
