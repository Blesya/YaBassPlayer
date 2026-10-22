using System.Text;
using Net.Codecrete.QrCodeGenerator;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Рисует QR-код половинными блоками: одна строка терминала несёт два ряда модулей,
/// поэтому код остаётся компактным по высоте. Вокруг символа — тихая зона в 2 модуля.
/// Берём низкий уровень коррекции: код показывается на чистом экране, а меньший размер
/// важнее — так он помещается в окно на обычном терминале.
/// </summary>
public sealed class QrCodeRenderer : IQrCodeRenderer
{
	private const int QuietZone = 2;

	/// <inheritdoc/>
	public string Render(string text)
	{
		QrCode qr = QrCode.EncodeText(text, QrCode.Ecc.Low);
		int size = qr.Size;
		int from = -QuietZone;
		int to = size + QuietZone;

		var builder = new StringBuilder();

		for (int y = from; y < to; y += 2)
		{
			for (int x = from; x < to; x++)
			{
				// GetModule считает координаты за пределами кода светлыми, поэтому
				// тихая зона и нечётная высота не требуют отдельных проверок.
				bool top = qr.GetModule(x, y);
				bool bottom = qr.GetModule(x, y + 1);

				builder.Append((top, bottom) switch
				{
					(true, true) => '█',
					(true, false) => '▀',
					(false, true) => '▄',
					_ => ' '
				});
			}

			builder.Append('\n');
		}

		return builder.ToString();
	}
}
