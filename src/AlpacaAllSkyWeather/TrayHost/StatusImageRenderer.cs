namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>Bridges the background Telegram command listener (runs on a thread-pool thread) to the
/// WinForms UI thread that owns <see cref="StatusForm"/>: <see cref="TrayApplicationContext"/> sets
/// <see cref="Render"/> once, and callers await <see cref="RenderAsync"/> to get a PNG screenshot of
/// the status window without touching any WinForms control off-thread.</summary>
public sealed class StatusImageRenderer
{
    public Func<CancellationToken, Task<byte[]?>>? Render { get; set; }

    public Task<byte[]?> RenderAsync(CancellationToken cancellationToken)
        => Render is null ? Task.FromResult<byte[]?>(null) : Render(cancellationToken);
}
