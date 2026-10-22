using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Events;

/// <summary>Published when the currently playing track changes.</summary>
public sealed record TrackChangedEvent(TrackKey Key);

/// <summary>Воспроизведение трека началось.</summary>
public sealed record TrackStartedEvent(TrackKey Key);

/// <summary>Трек доиграл до конца.</summary>
public sealed record TrackFinishedEvent(TrackKey Key, double PlayedSeconds);

/// <summary>Трек пропущен пользователем.</summary>
public sealed record TrackSkippedEvent(TrackKey Key, double PlayedSeconds);

/// <summary>Published when a playlist is selected/activated.</summary>
public sealed record PlaylistSelectedEvent(YamBassPlayer.Models.Playlist Playlist);

/// <summary>Published when a track is added to or removed from favorites.</summary>
public sealed record TrackFavoriteChangedEvent(TrackKey Key, bool IsFavorite);

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

/// <summary>Переключить режим воспроизведения: поочерёдно, случайно или повтор.</summary>
public sealed record PlaybackModeCommandEvent(PlaybackMode Mode);

/// <summary>
/// Поиск по медиатеке. Source задаёт источник, Kind — режим выдачи:
/// треки, либо треки первого исполнителя/альбома (только для ya).
/// </summary>
public sealed record SearchCommandEvent(SourceId Source, string Query, SearchEntityKind Kind = SearchEntityKind.Tracks);

/// <summary>Переключить избранное для трека в указанном источнике избранного.</summary>
public sealed record LikeCommandEvent(TrackKey Key);

/// <summary>Открыть окно справки по командам. HelpText содержит описание команд.</summary>
public sealed record HelpCommandEvent(string HelpText);
