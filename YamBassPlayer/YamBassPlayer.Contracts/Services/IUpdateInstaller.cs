namespace YamBassPlayer.Services;

/// <summary>
/// Откладывает установку скачанного архива до момента выхода из приложения:
/// подменять работающий exe нельзя, поэтому апдейтер стартует уже после закрытия UI.
/// </summary>
public interface IUpdateInstaller
{
	/// <summary>Запоминает архив, который нужно поставить при выходе.</summary>
	void Schedule(string zipPath, string version);

	/// <summary>Запускает апдейтер, если установка была запланирована. Вызывается перед завершением процесса.</summary>
	void LaunchPending();
}
