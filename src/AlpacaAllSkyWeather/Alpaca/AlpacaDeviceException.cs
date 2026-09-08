using ASCOM.Common.Alpaca;

namespace AlpacaAllSkyWeather.Alpaca;

public sealed class AlpacaDeviceException : Exception
{
    public AlpacaErrors ErrorNumber { get; }

    public AlpacaDeviceException(AlpacaErrors errorNumber, string message)
        : base(message)
    {
        ErrorNumber = errorNumber;
    }
}
