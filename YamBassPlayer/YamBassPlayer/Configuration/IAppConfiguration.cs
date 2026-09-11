namespace YamBassPlayer.Configuration;

/// <summary>
/// Абстракция над статическим <see cref="AppConfiguration"/> для внедрения через DI
/// и детерминированной подмены в тестах (без записи в <c>appsettings.json</c>).
/// </summary>
public interface IAppConfiguration
{
    float[] GetEqualizerBands();
    void SaveEqualizerBands(float[] bands);
}
