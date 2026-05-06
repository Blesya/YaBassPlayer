using YamBassPlayer.Enums;

namespace YamBassPlayer.Services.Events;

/// <summary>Published when the currently playing track changes.</summary>
public sealed record TrackChangedEvent(string TrackId);

/// <summary>Published when a playlist is selected/activated.</summary>
public sealed record PlaylistSelectedEvent(YamBassPlayer.Models.Playlist Playlist);

/// <summary>Published when a track is added to or removed from favorites.</summary>
public sealed record TrackFavoriteChangedEvent(string TrackId, string SourceId, bool IsFavorite);

/// <summary>Published when playback starts, pauses, resumes, or stops.</summary>
public sealed record PlaybackStateChangedEvent(PlaybackState State);

public enum PlaybackState
{
    Playing,
    Paused,
    Stopped,
    Resumed
}

// ── Командные интенты (CommandInputView) ─────────────────────────────────

/// <summary>Возобновить воспроизведение (play без аргумента).</summary>
public sealed record ResumeCommandEvent;

/// <summary>Пауза (play/pause).</summary>
public sealed record PlayPauseCommandEvent;

/// <summary>Пауза (pause).</summary>
public sealed record PauseCommandEvent;

/// <summary>Остановка.</summary>
public sealed record StopCommandEvent;

/// <summary>Следующий трек.</summary>
public sealed record NextCommandEvent;

/// <summary>Предыдущий трек.</summary>
public sealed record PreviousCommandEvent;

/// <summary>Перезапуск текущего трека.</summary>
public sealed record RestartCommandEvent;

/// <summary>Перемотка на процент (0-100).</summary>
public sealed record SeekCommandEvent(int Percent);

/// <summary>Воспроизвести N-й (0-based) трек текущего плейлиста.</summary>
public sealed record PlayTrackAtCommandEvent(int Index);

/// <summary>Переключить режим воспроизведения (true — случайно).</summary>
public sealed record ShuffleCommandEvent(bool Shuffle);

/// <summary>
/// Поиск по медиатеке. Source: "local" или "yandex".
/// Kind задаёт режим выдачи: треки, либо треки первого исполнителя/альбома (только для ya).
/// </summary>
public sealed record SearchCommandEvent(string Source, string Query, SearchEntityKind Kind = SearchEntityKind.Tracks);

/// <summary>Переключить избранное для текущего трека в указанном источнике.</summary>
public sealed record LikeCommandEvent(string SourceId, string TrackId);

/// <summary>Открыть окно справки по командам. HelpText содержит описание команд.</summary>
public sealed record HelpCommandEvent(string HelpText);
