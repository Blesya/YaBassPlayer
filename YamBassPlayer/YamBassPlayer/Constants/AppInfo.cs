using System.Reflection;

namespace YamBassPlayer.Constants;

/// <summary>
/// Сведения о сборке, нужные и окну «О программе», и проверке обновлений:
/// версия берётся из <c>Directory.Build.props</c> через <see cref="AssemblyInformationalVersionAttribute"/>.
/// </summary>
public static class AppInfo
{
	public const string RepositoryOwner = "Blesya";
	public const string RepositoryName = "YaBassPlayer";
	public const string RepositoryUrl = "https://github.com/Blesya/YaBassPlayer";

	/// <summary>Имя приложения: из него собирается имя exe и ассета в релизе. С именем репозитория не совпадает.</summary>
	public const string AppName = "YamBassPlayer";

	public const string AppExeName = "YamBassPlayer.exe";
	public const string UpdaterExeName = "YamBassPlayer.Updater.exe";

	/// <summary>Версия сборки без суффикса коммита: «0.0.1».</summary>
	public static string Version { get; } = ResolveVersion();

	/// <summary>Заголовок окна на старте: имя приложения и версия, «YamBassPlayer 0.0.1».</summary>
	public static string WindowTitle { get; } = $"{AppName} {Version}";

	/// <summary>Версия для сравнения с релизом; <c>null</c>, если версия не парсится.</summary>
	public static System.Version? ParsedVersion { get; } =
		System.Version.TryParse(Version, out System.Version? parsed) ? parsed : null;

	private static string ResolveVersion()
	{
		Assembly assembly = typeof(AppInfo).Assembly;

		string? informational = assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
			.InformationalVersion;

		string version = informational
			?? assembly.GetName().Version?.ToString()
			?? "1.0.0";

		// Отбрасываем суффикс сборки вида «1.0.0+<commit>», если он есть.
		int plusIndex = version.IndexOf('+');
		return plusIndex >= 0 ? version[..plusIndex] : version;
	}
}
