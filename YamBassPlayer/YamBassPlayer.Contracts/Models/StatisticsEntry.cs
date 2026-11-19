namespace YamBassPlayer.Models;

/// <summary>Строка рейтинга: название (артист, альбом, жанр, десятилетие) и число прослушиваний.</summary>
public sealed record StatisticsEntry(string Name, int Count);
