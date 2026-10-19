namespace YamBassPlayer.Configuration;

/// <summary>
/// DI-обёртка над статическим <see cref="AppConfiguration"/>.
/// </summary>
public sealed class AppConfigurationProvider : IAppConfiguration
{
    public float[] GetEqualizerBands() => AppConfiguration.GetEqualizerBands();

    public void SaveEqualizerBands(float[] bands) => AppConfiguration.SaveEqualizerBands(bands);

    public string? GetYandexMusicToken() => AppConfiguration.GetYandexMusicToken();

    public void SaveToken(string token) => AppConfiguration.SaveToken(token);

    public string? GetRefreshToken() => AppConfiguration.GetRefreshToken();

    public void SaveRefreshToken(string refreshToken) => AppConfiguration.SaveRefreshToken(refreshToken);

    public string GetOrCreateDeviceId() => AppConfiguration.GetOrCreateDeviceId();
}
