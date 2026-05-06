using System.Globalization;
using YamBassPlayer.Services;

namespace YamBassPlayer.Commands;

/// <summary>
/// eq — работа с эквалайзером.
/// Без аргументов — показать текущие значения; eq reset — сброс;
/// eq &lt;band 1-10&gt; &lt;gain 0-10&gt; — установить полосу.
/// </summary>
public sealed class EqualizerCommand(IBassEqualizer bassEqualizer) : ICommand
{
	private const int BandCount = 10;

	public string Name => "eq";
	public string Description => "eq — эквалайзер: eq / eq reset / eq <1-10> <0-10>.";
	public IReadOnlyList<string> Aliases => [];

	public CommandResult Execute(string[] args)
	{
		if (args.Length == 0)
		{
			var bands = bassEqualizer.GetBands();
			string text = string.Join(", ", bands.Select(b => b.ToString("0.##", CultureInfo.InvariantCulture)));
			return CommandResult.Ok($"Полосы: {text}");
		}

		if (args.Length == 1 && string.Equals(args[0], "reset", StringComparison.OrdinalIgnoreCase))
		{
			for (int i = 0; i < BandCount; i++)
				bassEqualizer.SetBand(i, 0f);
			return CommandResult.Ok("Эквалайзер сброшен");
		}

		if (args.Length < 2)
			return CommandResult.Error("Формат: eq <полоса 1-10> <усиление 0-10> или eq reset");

		if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int band)
			|| band < 1 || band > BandCount)
		{
			return CommandResult.Error($"Некорректная полоса. Ожидается число от 1 до {BandCount}");
		}

		if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float gain)
			|| gain < 0f || gain > 10f)
		{
			return CommandResult.Error("Некорректное усиление. Ожидается число от 0 до 10");
		}

		bassEqualizer.SetBand(band - 1, gain / 10f);
		return CommandResult.Ok($"Полоса {band}: {gain:0.#}");
	}
}
