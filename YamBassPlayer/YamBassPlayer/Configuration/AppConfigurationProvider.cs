namespace YamBassPlayer.Configuration;

/// <summary>
/// DI-обёртка над статическим <see cref="AppConfiguration"/>.
/// </summary>
public sealed class AppConfigurationProvider : IAppConfiguration
{
    public float[] GetEqualizerBands() => AppConfiguration.GetEqualizerBands();

    public void SaveEqualizerBands(float[] bands) => AppConfiguration.SaveEqualizerBands(bands);
}
