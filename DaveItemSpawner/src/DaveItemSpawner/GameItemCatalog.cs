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
                _log.LogDebug("DataManager unavailable: no instance.");
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
            _log.LogDebug("DataManager unavailable: game data is not loaded.");
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
        var route = GuessRouteFromItem(DataManager.hasInstance ? DataManager.Instance : null, tid);
        return new ItemEntry(tid, $"TID {tid}", string.Empty, -1, route);
    }

    private void AddIntegratedItems(DataManager dataManager, System.Collections.Generic.Dictionary<int, ItemEntry> rows)
    {
        object? dict;
        try
        {
            dict = dataManager.IntegratedItemDic;
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
                var item = value as IntegratedItem;
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
                var label = BuildLabel(dataManager, tid, textId, item.SpawnObject ?? string.Empty);
                var route = GuessRouteFromItem(dataManager, tid);
                rows[tid] = new ItemEntry(tid, label, textId, item.IntegratedType, route);
            }
        }
        catch (Exception ex)
        {
            LogDebugException("IntegratedItemDic enumeration failed", ex);
        }
    }

    private void AddItemBaseRows(DataManager dataManager, System.Collections.Generic.Dictionary<int, ItemEntry> rows)
    {
        object? dict;
        try
        {
            dict = dataManager.ItemBaseDataDic;
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
                var item = value as DR.IItemBase;
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
                var label = BuildLabel(dataManager, tid, textId, string.Empty);
                var route = GuessRouteFromItem(dataManager, tid);
                rows[tid] = new ItemEntry(tid, label, textId, item.ItemType, route);
            }
        }
        catch (Exception ex)
        {
            LogDebugException("ItemBaseDataDic enumeration failed", ex);
        }
    }

    public ItemAddRoute GuessRouteFromItem(DataManager? dataManager, int tid)
    {
        if (dataManager is not null)
        {
            try
            {
                var item = dataManager.GetItems(tid);
                var ingredientTid = DataManager.GetIngredientTIDFromItemTID(tid);
                return ItemRouting.ResolveRoute(item?.ItemType ?? 0, ingredientTid);
            }
            catch (Exception ex)
            {
                LogDebugException($"Route detection failed for TID {tid}", ex);
                return ItemAddRoute.Inventory;
            }
        }

        return ItemAddRoute.Inventory;
    }

    private static string BuildLabel(DataManager dataManager, int tid, string textId, string spawnObject)
    {
        if (!string.IsNullOrWhiteSpace(textId))
        {
            try
            {
                var localized = dataManager.GetText(textId);
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
