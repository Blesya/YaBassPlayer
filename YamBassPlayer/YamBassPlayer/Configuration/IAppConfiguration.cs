namespace YamBassPlayer.Configuration;

/// <summary>
/// Абстракция над статическим <see cref="AppConfiguration"/> для внедрения через DI
/// и детерминированной подмены в тестах (без записи в <c>appsettings.json</c>).
/// </summary>
public interface IAppConfiguration
{
    float[] GetEqualizerBands();
    void SaveEqualizerBands(float[] bands);

    /// <summary>Токен Яндекс.Музыки из конфига, либо <c>null</c>.</summary>
    string? GetYandexMusicToken();

    /// <summary>Сохраняет токен Яндекс.Музыки в конфиг.</summary>
    void SaveToken(string token);

    /// <summary>Refresh-токен Яндекс.Музыки из конфига, либо <c>null</c>.</summary>
    string? GetRefreshToken();

    /// <summary>Сохраняет refresh-токен Яндекс.Музыки в конфиг.</summary>
    void SaveRefreshToken(string refreshToken);

    /// <summary>
    /// Идентификатор устройства для OAuth Device Flow. Генерируется при первом обращении
    /// и дальше переиспользуется, чтобы Яндекс не копил токены под разными device_id.
    /// </summary>
    string GetOrCreateDeviceId();
}
