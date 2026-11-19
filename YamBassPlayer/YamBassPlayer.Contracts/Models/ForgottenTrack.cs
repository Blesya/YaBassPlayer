namespace YamBassPlayer.Models;

/// <summary>Трек, который когда-то слушали часто, а сейчас забросили.</summary>
public sealed record ForgottenTrack(string Name, int Listens, DateOnly LastListened);
