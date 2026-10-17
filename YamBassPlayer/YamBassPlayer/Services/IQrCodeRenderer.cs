namespace YamBassPlayer.Services;

/// <summary>Рисует QR-код как текст, пригодный для вывода в терминале.</summary>
public interface IQrCodeRenderer
{
	/// <summary>Возвращает QR-код для <paramref name="text"/> в виде многострочного текста.</summary>
	string Render(string text);
}
