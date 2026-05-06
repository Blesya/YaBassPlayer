namespace YamBassPlayer.Views;

public interface ICommandInputView
{
	event Action<string>? OnCommandSubmitted;
	void SetResult(string result);
	void FocusInput();
}
