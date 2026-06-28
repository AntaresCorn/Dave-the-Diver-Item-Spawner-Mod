using System.Reflection;
using UnityEngine.SceneManagement;

namespace DaveItemSpawner;

internal static class InteropAccess
{
    // Reflection facade for game internals whose signatures vary across builds.
    public static bool TryGetDataManager(out object? dataManager)
    {
        return TryGetSingleton(typeof(DataManager), out dataManager);
    }

    public static bool IsGameDataLoaded(object dataManager)
    {
        return GetIntProperty(dataManager, "IsGameDataLoaded", "get_IsGameDataLoaded") != 0;
    }

    public static object? GetIntegratedItemDictionary(object dataManager)
    {
        return GetPropertyOrFieldValue(dataManager, "IntegratedItemDic");
    }

    public static object? GetItemBaseDataDictionary(object dataManager)
    {
        return GetPropertyOrFieldValue(dataManager, "ItemBaseDataDic");
    }

    public static object? GetItemByTid(object dataManager, int tid)
    {
        // Different versions expose different entry points; try the common ones in order.
        return TryInvokeBestMethod(dataManager, "GetItems", tid) ??
            TryInvokeBestMethod(dataManager, "GetItemV2", tid) ??
            TryInvokeBestMethod(dataManager, "GetItemBase", tid);
    }

    public static int GetIngredientTidFromItemTid(int tid)
    {
        return InvokeIntBestMethod(typeof(DataManager), "GetIngredientTIDFromItemTID", tid);
    }

    public static string? GetLocalizedText(object dataManager, string textId)
    {
        return InvokeBestMethod(dataManager, "GetText", textId)?.ToString();
    }

    public static bool TryGetSaveSystem(out object? saveSystem)
    {
        return TryGetSingleton(typeof(DR.Save.SaveSystem), out saveSystem);
    }

    public static SaveData? GetGameSave()
    {
        return TryInvokeBestMethod(typeof(DR.Save.SaveSystem), "GetGameSave") as SaveData;
    }

    public static bool SaveGameData(object saveSystem)
    {
        // Keep compatibility with both SaveGameData and TrySaveGameData APIs.
        var result = TryInvokeBestMethod(saveSystem, "SaveGameData") ??
            TryInvokeBestMethod(saveSystem, "TrySaveGameData");
        return result is bool saved && saved;
    }

    public static bool TryGetIngredientsStorage(out object? ingredientsStorage)
    {
        return TryGetSingleton(typeof(IngredientsStorage), out ingredientsStorage);
    }

    public static void AddIngredients(object ingredientsStorage, int ingredientTid, int amount, SushiBar.Place place)
    {
        InvokeBestMethod(ingredientsStorage, "AddIngredients", ingredientTid, amount, place);
    }

    public static void AddInventoryItemSaveData(SaveData saveData, int tid, int amount)
    {
        // Some builds expect an extra nullable metadata argument.
        if (HasMethod(saveData.GetType(), "AddInventoryItemSaveData", tid, amount, null))
        {
            InvokeBestMethod(saveData, "AddInventoryItemSaveData", tid, amount, null);
            return;
        }

        InvokeBestMethod(saveData, "AddInventoryItemSaveData", tid, amount);
    }

    public static void UpdateInventoryItemSave(SaveData saveData)
    {
        InvokeBestMethod(saveData, "UpdateInventoryItemSave");
    }

    public static void UpdateIngredientsSave(SaveData saveData)
    {
        InvokeBestMethod(saveData, "UpdateIngredientsSave");
    }

    public static object? GetJungleSave(SaveData saveData)
    {
        return GetPropertyOrFieldValue(saveData, "JDLCContents");
    }

    public static void AddJungleVilItemSaveData(object jungleSave, int tid, int amount)
    {
        var itemId = Guid.NewGuid().ToString("N");
        InvokeBestMethod(jungleSave, "AddJungleVilItemSaveData", tid, amount, itemId, false, false, false);
    }

    public static void UpdateJungleVilItemSave(object jungleSave)
    {
        InvokeBestMethod(jungleSave, "UpdateJungleVilItemSave");
    }

    public static void UpdateJungleSave(object jungleSave)
    {
        TryInvokeBestMethod(jungleSave, "UpdateJungleSave");
    }

    public static bool IsJungleSceneActive()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrWhiteSpace(sceneName) &&
            sceneName.Contains("jungle", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetActiveSceneName()
    {
        return SceneManager.GetActiveScene().name ?? string.Empty;
    }

    public static int GetIntProperty(object source, params string[] memberNames)
    {
        foreach (var memberName in memberNames)
        {
            var value = GetPropertyOrFieldValue(source, memberName);
            if (value is null)
            {
                continue;
            }

            return Convert.ToInt32(value);
        }

        return 0;
    }

    public static string? GetStringProperty(object source, params string[] memberNames)
    {
        foreach (var memberName in memberNames)
        {
            var value = GetPropertyOrFieldValue(source, memberName);
            if (value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static bool TryGetSingleton(Type type, out object? instance)
    {
        // Most singleton wrappers expose hasInstance + Instance with these exact names.
        var hasInstance = GetPropertyOrFieldValue(null, type, "hasInstance");
        if (hasInstance is bool hasInstanceValue && !hasInstanceValue)
        {
            instance = null;
            return false;
        }

        instance = GetPropertyOrFieldValue(null, type, "Instance");
        return instance is not null;
    }

    private static object? GetPropertyOrFieldValue(object? source, string memberName)
    {
        return GetPropertyOrFieldValue(source, source?.GetType(), memberName);
    }

    private static object? GetPropertyOrFieldValue(object? source, Type? type, string memberName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (property is not null)
            {
                return property.GetValue(source);
            }

            var field = current.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field is not null)
            {
                return field.GetValue(source);
            }
        }

        return null;
    }

    private static int InvokeIntBestMethod(Type type, string methodName, params object?[] args)
    {
        var value = InvokeBestMethod(type, methodName, args);
        return value is null ? 0 : Convert.ToInt32(value);
    }

    private static object? InvokeBestMethod(object target, string methodName, params object?[] args)
    {
        return InvokeBestMethodCore(target.GetType(), target, methodName, args);
    }

    private static object? InvokeBestMethod(Type type, string methodName, params object?[] args)
    {
        return InvokeBestMethodCore(type, null, methodName, args);
    }

    private static object? InvokeBestMethodCore(Type type, object? target, string methodName, object?[] args)
    {
        var method = FindMethod(type, methodName, args);
        if (method is null)
        {
            throw new MissingMethodException(type.FullName, methodName);
        }

        return method.Invoke(target, args);
    }

    private static object? TryInvokeBestMethod(object target, string methodName, params object?[] args)
    {
        return TryInvokeBestMethodCore(target.GetType(), target, methodName, args);
    }

    private static object? TryInvokeBestMethod(Type type, string methodName, params object?[] args)
    {
        return TryInvokeBestMethodCore(type, null, methodName, args);
    }

    private static object? TryInvokeBestMethodCore(Type type, object? target, string methodName, object?[] args)
    {
        var method = FindMethod(type, methodName, args);
        return method?.Invoke(target, args);
    }

    private static MethodInfo? FindMethod(Type type, string methodName, object?[] args)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            // Select by name + arity first, then strict compatibility check.
            var methods = current
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal) &&
                    method.GetParameters().Length == args.Length);

            foreach (var method in methods)
            {
                if (ParametersMatch(method.GetParameters(), args))
                {
                    return method;
                }
            }
        }

        return null;
    }

    private static bool HasMethod(Type type, string methodName, params object?[] args)
    {
        return FindMethod(type, methodName, args) is not null;
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object?[] args)
    {
        // Allows nullables/enums and avoids invalid reflection invokes on mismatched overloads.
        for (var index = 0; index < parameters.Length; index++)
        {
            var argument = args[index];
            var parameterType = parameters[index].ParameterType;

            if (argument is null)
            {
                if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) is null)
                {
                    return false;
                }

                continue;
            }

            var argumentType = argument.GetType();
            if (parameterType.IsAssignableFrom(argumentType))
            {
                continue;
            }

            var underlyingType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;
            if (underlyingType.IsEnum && argumentType == Enum.GetUnderlyingType(underlyingType))
            {
                continue;
            }

            if (underlyingType == typeof(int) && argumentType == typeof(int))
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
