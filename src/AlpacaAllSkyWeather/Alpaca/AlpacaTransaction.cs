namespace AlpacaAllSkyWeather.Alpaca;

public static class AlpacaTransaction
{
    private static long _serverTransactionCounter;

    public static uint NextServerTransactionId()
        => unchecked((uint)Interlocked.Increment(ref _serverTransactionCounter));

    public static async Task<uint> GetClientTransactionIdAsync(HttpRequest request)
    {
        var raw = HttpMethods.IsPut(request.Method)
            ? (await request.ReadFormAsync())["ClientTransactionID"].ToString()
            : request.Query["ClientTransactionID"].ToString();

        return uint.TryParse(raw, out var value) ? value : 0;
    }
}
