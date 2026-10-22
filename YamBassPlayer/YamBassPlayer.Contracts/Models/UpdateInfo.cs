namespace YamBassPlayer.Models;

/// <summary>
/// Найденный релиз: что за версия, какой файл качать и где взять его контрольную сумму.
/// </summary>
/// <param name="Version">Версия релиза без префикса «v».</param>
/// <param name="TagName">Тег как он есть в GitHub: «v0.0.2».</param>
/// <param name="AssetName">Имя zip-архива дистрибутива.</param>
/// <param name="DownloadUrl">Прямая ссылка на архив.</param>
/// <param name="SizeBytes">Размер архива; 0, если GitHub его не сообщил.</param>
/// <param name="Notes">Тело релиза с описанием изменений.</param>
/// <param name="ChecksumUrl">Ссылка на файл с sha256; <c>null</c>, если ассета нет.</param>
public sealed record UpdateInfo(
	Version Version,
	string TagName,
	string AssetName,
	string DownloadUrl,
	long SizeBytes,
	string? Notes,
	string? ChecksumUrl);
