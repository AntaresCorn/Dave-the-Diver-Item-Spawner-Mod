using UnityEngine;

namespace DaveItemSpawner;

public sealed class PluginGui
{
    private readonly GameItemCatalog _catalog;
    private readonly GameItemAdder _adder;
    private readonly int _maxResults;
    private Rect _windowRect = new(60, 60, 760, 560);
    private Vector2 _scroll;
    private string _query = string.Empty;
    private string _amountText = "1";
    private string _status = "Ready.";
    private ItemEntry? _selected;

    public PluginGui(GameItemCatalog catalog, GameItemAdder adder, int maxResults)
    {
        _catalog = catalog;
        _adder = adder;
        _maxResults = Math.Clamp(maxResults, 1, 500);
    }

    public void Draw()
    {
        GUILayout.BeginArea(_windowRect, GUI.skin.window);
        DrawPanel();
        GUILayout.EndArea();
    }

    private void DrawPanel()
    {
        GUILayout.BeginVertical();

        if (!_catalog.TryRefresh())
        {
            GUILayout.Label("Waiting for game data...");
            GUILayout.EndVertical();
            return;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", GUILayout.Width(60));
        _query = GUILayout.TextField(_query);
        GUILayout.Label("Amount", GUILayout.Width(60));
        _amountText = GUILayout.TextField(_amountText, GUILayout.Width(80));
        GUILayout.EndHorizontal();

        var results = ItemSearch.Filter(_catalog.Entries, _query, _maxResults);
        GUILayout.Label($"Results: {results.Count}", GUILayout.Height(20));

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

        if (GUILayout.Button("Add TID", GUILayout.Height(32)))
        {
            AddTidFromSearch();
        }
        GUILayout.EndHorizontal();

        GUILayout.Label(_selected is null ? "Selected: none" : $"Selected: {_selected.Display}");
        GUILayout.Label(_status);
        GUILayout.EndVertical();
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
        _query = tid.ToString();

        var result = _adder.Add(entry, amount);
        _status = result.Message;
    }

    private bool TryGetAmount(out int amount)
    {
        if (!ItemInput.TryParseAmount(_amountText, out amount))
        {
            _status = "Amount must be a positive integer.";
            return false;
        }

        _amountText = amount.ToString();
        return true;
    }
}
