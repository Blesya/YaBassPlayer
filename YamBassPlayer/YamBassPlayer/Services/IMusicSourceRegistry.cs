namespace YamBassPlayer.Services;

public interface IMusicSourceRegistry
{
    IReadOnlyList<IMusicSource> Sources { get; }
    void Register(IMusicSource source);
    IMusicSource? Get(string sourceId);
    IMusicSource GetRequired(string sourceId);
}
