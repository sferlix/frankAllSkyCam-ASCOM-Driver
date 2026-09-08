using ASCOM.Common.Alpaca;

namespace AlpacaAllSkyWeather.Alpaca;

public static class AlpacaEndpointHelpers
{
    public static async Task<IResult> HandleDoubleAsync(HttpRequest request, Func<double> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new DoubleResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new DoubleResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleStringAsync(HttpRequest request, Func<string> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new StringResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new StringResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleStringListAsync(HttpRequest request, Func<IList<string>> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new StringListResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new StringListResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleBoolAsync(HttpRequest request, Func<bool> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new BoolResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new BoolResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleIntAsync(HttpRequest request, Func<int> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new IntResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new IntResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleMethodAsync(HttpRequest request, Action action)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            action();
            return Results.Ok(new MethodResponse(clientId, serverId));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new MethodResponse(clientId, serverId) { ErrorNumber = ex.ErrorNumber, ErrorMessage = ex.Message });
        }
    }

    private static async Task<(uint ClientId, uint ServerId)> IdsAsync(HttpRequest request)
        => (await AlpacaTransaction.GetClientTransactionIdAsync(request), AlpacaTransaction.NextServerTransactionId());
}
