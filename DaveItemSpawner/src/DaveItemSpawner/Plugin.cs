using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using InputSystemWrapper;
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
    private ConfigEntry<float>? _windowOpacity;
    private PluginGui? _gui;
    private bool _visible;
    private bool _cursorStateCaptured;
    private bool _previousCursorVisible;
    private CursorLockMode _previousCursorLockState;
    private readonly List<InputAsset> _lockedInputAssets = new();
    private bool _gameInputLocked;
    private bool _inputAssetUnavailableLogged;

    // Initializes config and wires the in-game UI host component.
    public override void Load()
    {
        _toggleKey = Config.Bind("UI", "ToggleKey", KeyCode.F8, "Key used to show or hide the item spawner.");
        _maxResults = Config.Bind("UI", "MaxSearchResults", 100, "Maximum search results shown in the panel.");
        _windowOpacity = Config.Bind("UI", "WindowOpacity", 0.8f,
            "Opacity of the item spawner window from 0.1 (mostly transparent) to 1.0 (opaque).");

        var catalog = new GameItemCatalog(Log);
        var adder = new GameItemAdder(Log);
        _gui = new PluginGui(catalog, adder, _maxResults.Value, () => _windowOpacity.Value);

        AddComponent<ItemSpawnerBehaviour>().Init(this);
        Log.LogInfo($"{PluginName} loaded. Press {_toggleKey.Value} in game.");
    }

    private void Toggle()
    {
        _visible = !_visible;
        if (_visible)
        {
            ShowCursor();
            LockGameInput();
        }
        else
        {
            RestoreCursor();
            UnlockGameInput();
        }
    }

    private void Draw()
    {
        if (_visible)
        {
            ShowCursor();
            LockGameInput();
            _gui?.Draw();
        }
    }

    private void ShowCursor()
    {
        if (!_cursorStateCaptured)
        {
            _previousCursorVisible = Cursor.visible;
            _previousCursorLockState = Cursor.lockState;
            _cursorStateCaptured = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreCursor()
    {
        if (!_cursorStateCaptured)
        {
            return;
        }

        Cursor.visible = _previousCursorVisible;
        Cursor.lockState = _previousCursorLockState;
        _cursorStateCaptured = false;
    }

    private void LockGameInput()
    {
        if (_gameInputLocked)
        {
            return;
        }

        foreach (var inputAsset in Resources.FindObjectsOfTypeAll<InputAsset>())
        {
            if (inputAsset is null)
            {
                continue;
            }

            inputAsset.Lock();
            _lockedInputAssets.Add(inputAsset);
        }

        _gameInputLocked = _lockedInputAssets.Count > 0;
        if (!_gameInputLocked && !_inputAssetUnavailableLogged)
        {
            Log.LogWarning("No active game input assets were found; input cannot be blocked yet.");
            _inputAssetUnavailableLogged = true;
        }
    }

    private void UnlockGameInput()
    {
        foreach (var inputAsset in _lockedInputAssets)
        {
            if (inputAsset is not null)
            {
                inputAsset.UnLock();
            }
        }

        _lockedInputAssets.Clear();
        _gameInputLocked = false;
    }

    private sealed class ItemSpawnerBehaviour : MonoBehaviour
    {
        private Plugin? _plugin;
        private ManualLogSource? _log;

        // Unity-managed bridge so the plugin can receive Update/OnGUI callbacks.
        public void Init(Plugin plugin)
        {
            _plugin = plugin;
            _log = plugin.Log;
        }

        private void Update()
        {
            if (_plugin?._toggleKey is not null && UnityEngine.Input.GetKeyDown(_plugin._toggleKey.Value))
            {
                _plugin.Toggle();
            }
        }

        private void OnGUI()
        {
            try
            {
                // IMGUI exceptions can break rendering for the frame, so guard draw calls.
                _plugin?.Draw();
            }
            catch (Exception ex)
            {
                _log?.LogError(ex);
            }
        }
    }
}
