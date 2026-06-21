namespace DaveItemSpawner;

public static class ItemInput
{
    public static bool TryParseAmount(string? query, out int amount)
    {
        query = query?.Trim();
        if (!int.TryParse(query, out amount) || amount <= 0)
        {
            amount = 0;
            return false;
        }

        amount = Math.Min(amount, 9999);
        return true;
    }
}
