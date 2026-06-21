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
        _gui = new PluginGui(catalog, adder, _maxResults.Value);

        AddComponent<ItemSpawnerBehaviour>().Init(this);
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
        private Plugin? _plugin;
        private ManualLogSource? _log;

        public void Init(Plugin plugin)
        {
            _plugin = plugin;
            _log = plugin.Log;
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
