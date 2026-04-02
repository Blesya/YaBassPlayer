namespace YamBassPlayer.Services;

public interface IMusicSourceRegistry
{
    IReadOnlyList<IMusicSource> Sources { get; }
    IMusicSource? Get(string sourceId);
    IMusicSource GetRequired(string sourceId);
}
