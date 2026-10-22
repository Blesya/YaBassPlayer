using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Presenters;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Plugins;

/// <summary>Адаптер окна плагина к модальному хосту.</summary>
internal sealed class PluginModalView(Toplevel window) : IModalView
{
    public Toplevel ModalWindow => window;
}

/// <summary>
/// Хост-реализация UI для плагинов: маршалит в UI-поток, показывает окна
/// через общий модальный хост и отдаёт стандартные диалоги.
/// </summary>
public sealed class PluginUi(
    IUiDispatcher dispatcher,
    IModalWindowHost modalHost,
    IPlayStatusPresenter playStatusPresenter,
    ITrackRepository trackRepository,
    ITracksPresenter tracksPresenter,
    IPlaylistsPresenter playlistsPresenter,
    ITrackRepositoryCache trackRepositoryCache,
    IErrorHandler errorHandler) : IPluginUi
{
    public IUiDispatcher Dispatcher => dispatcher;

    public IUserPrompts Prompts { get; } = new UserPrompts(dispatcher);

    public void SetStatus(string status) => playStatusPresenter.SetPlayStatus(status);

    public Task ShowWindowAsync(IPluginWindow window, CancellationToken ct = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        dispatcher.Invoke(() =>
        {
            try
            {
                modalHost.Show(new PluginModalView(window.CreateWindow()));
            }
            catch (Exception ex)
            {
                errorHandler.Handle(ex);
            }
            finally
            {
                completion.TrySetResult();
            }
        });

        return completion.Task;
    }

    public async Task ShowPlaylistAsync(Playlist playlist, IReadOnlyList<Track> tracks, CancellationToken ct = default)
    {
        if (playlist.SourceId is not { } sourceId)
            throw new ArgumentException("Плейлист плагина обязан указывать SourceId", nameof(playlist));

        trackRepositoryCache.ReplaceSearchTracks(sourceId, tracks);
        await trackRepository.SetPlaylist(playlist, ct);
        await tracksPresenter.LoadTracksFor(playlist);
        playlistsPresenter.NotifyTransientPlaylistActive(playlist);
    }
}

/// <summary>Стандартные диалоги хоста для плагинов.</summary>
internal sealed class UserPrompts(IUiDispatcher dispatcher) : IUserPrompts
{
    public Task MessageAsync(string title, string message, CancellationToken ct = default)
    {
        dispatcher.Invoke(() => MessageBox.Query(title, message, "OK"));
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, CancellationToken ct = default)
    {
        bool result = false;
        dispatcher.Invoke(() => result = MessageBox.Query(title, message, "Да", "Нет") == 0);
        return Task.FromResult(result);
    }

    public Task<string?> InputAsync(string title, string prompt, string? initial = null, CancellationToken ct = default)
    {
        string? result = null;

        dispatcher.Invoke(() =>
        {
            var field = new TextField(initial ?? string.Empty)
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill() - 2
            };
            var dialog = new Dialog(title, 60, 8);
            dialog.Add(new Label(prompt) { X = 1, Y = 0 }, field);
            var ok = new Button("OK", is_default: true);
            ok.Clicked += () => { result = field.Text?.ToString(); Application.RequestStop(dialog); };
            var cancel = new Button("Отмена");
            cancel.Clicked += () => Application.RequestStop(dialog);
            dialog.AddButton(ok);
            dialog.AddButton(cancel);
            Application.Run(dialog);
        });

        return Task.FromResult(result);
    }

    public Task<T?> SelectAsync<T>(string title, IReadOnlyList<T> items, Func<T, string> label, CancellationToken ct = default)
    {
        T? result = default;

        dispatcher.Invoke(() =>
        {
            var list = new ListView(items.Select(label).ToList())
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill() - 2,
                Height = Dim.Fill() - 3
            };
            var dialog = new Dialog(title, 60, 16);
            dialog.Add(list);
            var ok = new Button("OK", is_default: true);
            ok.Clicked += () =>
            {
                if (list.SelectedItem >= 0 && list.SelectedItem < items.Count)
                    result = items[list.SelectedItem];
                Application.RequestStop(dialog);
            };
            var cancel = new Button("Отмена");
            cancel.Clicked += () => Application.RequestStop(dialog);
            dialog.AddButton(ok);
            dialog.AddButton(cancel);
            Application.Run(dialog);
        });

        return Task.FromResult(result);
    }
}
