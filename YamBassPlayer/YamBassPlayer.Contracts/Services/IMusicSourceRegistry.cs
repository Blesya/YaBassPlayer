using YamBassPlayer.Models;

namespace YamBassPlayer.Services;

public interface IMusicSourceRegistry
{
    IReadOnlyList<IMusicSource> Sources { get; }
    IMusicSource? Get(SourceId id);
    IMusicSource GetRequired(SourceId id);

    /// <summary>Добавляет источник плагина после сборки контейнера.</summary>
    void Add(IMusicSource source);
}
