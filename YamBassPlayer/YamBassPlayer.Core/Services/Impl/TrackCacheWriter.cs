using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Запись треков в кэш: одиночная и пакетная (в своей транзакции). Обе берут блокировку
/// записи и открывают соединение сами.
/// </summary>
internal sealed class TrackCacheWriter
{
	private readonly IDbConnectionFactory _connectionFactory;
	private readonly IDbWriteLock _writeLock;

	public TrackCacheWriter(IDbConnectionFactory connectionFactory, IDbWriteLock writeLock)
	{
		_connectionFactory = connectionFactory;
		_writeLock = writeLock;
	}

	public async Task SaveAsync(Track track)
	{
		using var lockHandle = await _writeLock.AcquireAsync();
		using var connection = _connectionFactory.Create();
		using var writer = new TrackWriteCommands(connection, null);
		await writer.WriteAsync(track);
	}

	public async Task SaveBatchAsync(IReadOnlyList<Track> tracks)
	{
		using var lockHandle = await _writeLock.AcquireAsync();
		using var connection = _connectionFactory.Create();
		using var transaction = connection.BeginTransaction();
		using var writer = new TrackWriteCommands(connection, transaction);
		foreach (Track track in tracks)
			await writer.WriteAsync(track);
		transaction.Commit();
	}
}
