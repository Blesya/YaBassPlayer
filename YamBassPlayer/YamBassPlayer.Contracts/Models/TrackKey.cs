namespace YamBassPlayer.Models;

/// <summary>
/// Каноническая ссылка на трек: идентификатор трека уникален только внутри источника.
/// </summary>
public readonly record struct TrackKey(SourceId SourceId, string TrackId)
{
    public override string ToString() => $"{SourceId}:{TrackId}";
}
