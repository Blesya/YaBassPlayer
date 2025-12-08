namespace YamBassPlayer.Services.Impl;

public sealed class ListenTimer : IListenTimer
{
    private readonly IHistoryService _historyService;
    private CancellationTokenSource? _cts;
    private TimeSpan _remaining = TimeSpan.FromSeconds(30);
    private DateTime _lastPlayUtc;
    private string? _trackId;

    public ListenTimer(IHistoryService historyService)
    {
        _historyService = historyService;
    }

    public void OnTrackStart(string trackId)
    {
        ResetInternal();
        _trackId = trackId;
        StartCountdown();
    }

    public void OnPause()
    {
        if (_cts == null)
            return;

        var now = DateTime.UtcNow;
        var delta = now - _lastPlayUtc;
        _remaining -= delta;

        _cts.Cancel();
    }

    public void OnResume()
    {
        if (_remaining <= TimeSpan.Zero)
            return;

        StartCountdown();
    }

    public void OnTrackStopOrChange()
    {
        ResetInternal();
    }

    private void StartCountdown()
    {
        _cts = new CancellationTokenSource();
        _lastPlayUtc = DateTime.UtcNow;

        var localCts = _cts;
        var track = _trackId!;
        var rem = _remaining;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(rem, localCts.Token);
                LogListen(track);
                ResetInternal();
            }
            catch (TaskCanceledException)
            {
            }
        }, localCts.Token);
    }

    private void ResetInternal()
    {
        _cts?.Cancel();
        _cts = null;
        _remaining = TimeSpan.FromSeconds(30);
        _trackId = null;
    }

    private void LogListen(string trackId)
    {
        _historyService.LogListen(trackId);
    }
}