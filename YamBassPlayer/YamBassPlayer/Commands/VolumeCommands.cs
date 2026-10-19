using System.Globalization;
using YamBassPlayer.Services;

namespace YamBassPlayer.Commands;

/// <summary>
/// volume|vol — громкость. Без аргументов показывает текущее значение,
/// volume 70 — устанавливает абсолютное, volume +5 / volume -5 — меняет на шаг.
/// </summary>
public sealed class VolumeCommand(IAudioPlayer audioPlayer) : ICommand
{
	public string Name => "volume";
	public string Description => "volume|vol — громкость: volume / volume 0-100 / volume +N|-N.";
	public IReadOnlyList<string> Aliases => ["vol"];

	public CommandResult Execute(string[] args)
	{
		if (args.Length == 0)
			return CommandResult.Ok($"Громкость: {audioPlayer.VolumePercent}%");

		string arg = args[0];
		if (!int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
			return CommandResult.Error("Некорректная громкость. Ожидается 0-100 или +N/-N");

		bool relative = arg[0] is '+' or '-';
		int target = relative ? audioPlayer.VolumePercent + value : value;

		audioPlayer.SetVolumePercent(Math.Clamp(target, 0, 100));
		return CommandResult.Ok($"Громкость: {audioPlayer.VolumePercent}%");
	}
}
