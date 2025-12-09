namespace YamBassPlayer.Views;

public interface IPlayStatusView
{
    event Action? OnPlayClicked;
    event Action? OnStopClicked;
    event Action? OnPrevClicked;
    event Action? OnNextClicked;
    event Action<int>? OnSeekRequested;
    void SetPlayStatus(string status);
    void SetProgress(int percent);
    void SetTime(TimeSpan current, TimeSpan duration);
    void SetTitle(string title);
}