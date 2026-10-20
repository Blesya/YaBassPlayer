using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>
/// Один шаг схемы БД. Миграции применяются цепочкой по возрастанию версий,
/// каждая — в своей транзакции; версия БД хранится в <c>PRAGMA user_version</c>.
/// </summary>
public interface IDatabaseMigration
{
	/// <summary>Версия БД, с которой применяется шаг.</summary>
	int FromVersion { get; }

	/// <summary>Версия БД после успешного применения шага.</summary>
	int ToVersion { get; }

	/// <summary>Короткое описание для лога.</summary>
	string Description { get; }

	/// <summary>Изменяет схему. Вызывается внутри транзакции.</summary>
	void Up(SqliteConnection connection, SqliteTransaction transaction);
}
