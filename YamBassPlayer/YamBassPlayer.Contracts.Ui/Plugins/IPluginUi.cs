using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Services;

namespace YamBassPlayer.Plugins;

/// <summary>
/// Что плагин может показать пользователю. Окна запускает только хост:
/// иначе разъедется модальность и панель управления.
/// </summary>
public interface IPluginUi
{
    IUiDispatcher Dispatcher { get; }

    Task ShowWindowAsync(IPluginWindow window, CancellationToken ct = default);

    /// <summary>Показать треки как временный плейлист под узлом источника.</summary>
    Task ShowPlaylistAsync(Playlist playlist, IReadOnlyList<Track> tracks, CancellationToken ct = default);

    void SetStatus(string status);

    IUserPrompts Prompts { get; }
}

/// <summary>Окно плагина: плагин строит его сам, хост показывает модально.</summary>
public interface IPluginWindow
{
    string Title { get; }
    Toplevel CreateWindow();
}

/// <summary>Стандартные диалоги хоста, чтобы плагин не изобретал свои.</summary>
public interface IUserPrompts
{
    Task MessageAsync(string title, string message, CancellationToken ct = default);
    Task<bool> ConfirmAsync(string title, string message, CancellationToken ct = default);
    Task<string?> InputAsync(string title, string prompt, string? initial = null, CancellationToken ct = default);
    Task<T?> SelectAsync<T>(string title, IReadOnlyList<T> items, Func<T, string> label, CancellationToken ct = default);
}
