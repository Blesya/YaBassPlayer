using Serilog;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Одноразовый перенос плоских <c>tracks/*.mp3</c> в <c>tracks/&lt;sourceId&gt;/</c>.
/// Идемпотентен: существующий целевой файл не перезаписывается, исходник при ошибке
/// остаётся на месте, поэтому прерывание не теряет данные.
/// </summary>
public sealed class TrackCacheMigrator(string tracksFolder, SourceId flatFilesSourceId) : ITrackCacheMigrator
{
	public void Migrate()
	{
		if (!Directory.Exists(tracksFolder))
			return;

		string targetFolder = Path.Combine(tracksFolder, flatFilesSourceId.Value);
		int moved = 0;
		int skipped = 0;

		foreach (string sourcePath in Directory.GetFiles(tracksFolder, "*.mp3", SearchOption.TopDirectoryOnly))
		{
			string destinationPath = Path.Combine(targetFolder, Path.GetFileName(sourcePath));

			try
			{
				if (File.Exists(destinationPath))
				{
					skipped++;
					Log.Warning("Кэш треков: файл уже есть в папке источника, оставлен плоский {Path}", sourcePath);
					continue;
				}

				Directory.CreateDirectory(targetFolder);
				File.Move(sourcePath, destinationPath);
				moved++;
			}
			catch (Exception ex)
			{
				skipped++;
				Log.Warning(ex, "Кэш треков: не удалось перенести {Path}", sourcePath);
			}
		}

		if (moved > 0 || skipped > 0)
			Log.Information("Кэш треков разложен по источникам: перенесено {Moved}, пропущено {Skipped}", moved, skipped);
	}
}
