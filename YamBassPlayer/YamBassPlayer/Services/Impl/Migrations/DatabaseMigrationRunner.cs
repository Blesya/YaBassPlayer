using Microsoft.Data.Sqlite;
using Serilog;

namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>
/// Применяет зарегистрированные миграции по цепочке, ориентируясь на
/// <c>PRAGMA user_version</c>. Версия обновляется только после успешного
/// выполнения шага и в той же транзакции, поэтому прерванная миграция
/// не оставляет БД «наполовину обновлённой».
/// </summary>
public sealed class DatabaseMigrationRunner
{
	private readonly IReadOnlyList<IDatabaseMigration> _migrations;

	public DatabaseMigrationRunner(IReadOnlyList<IDatabaseMigration> migrations)
	{
		_migrations = migrations.OrderBy(m => m.FromVersion).ToArray();
		Validate(_migrations);
	}

	public int Migrate(SqliteConnection connection, int currentVersion, int targetVersion)
	{
		if (currentVersion > targetVersion)
			throw new InvalidOperationException(
				$"База данных новее приложения: версия {currentVersion}, приложение поддерживает до {targetVersion}.");

		int version = currentVersion;

		foreach (IDatabaseMigration migration in _migrations)
		{
			if (migration.ToVersion <= version)
				continue;

			if (migration.FromVersion != version)
				throw new InvalidOperationException(
					$"Нет миграции с версии {version} (следующая ожидает {migration.FromVersion}→{migration.ToVersion}).");

			using var transaction = connection.BeginTransaction();

			try
			{
				migration.Up(connection, transaction);
				SetUserVersion(connection, transaction, migration.ToVersion);
				transaction.Commit();
			}
			catch
			{
				transaction.Rollback();
				throw;
			}

			version = migration.ToVersion;
			Log.Information(
				"Миграция БД {From}→{To}: {Description}",
				migration.FromVersion,
				migration.ToVersion,
				migration.Description);
		}

		if (version != targetVersion)
			throw new InvalidOperationException(
				$"БД осталась на версии {version}, ожидалась {targetVersion}. Проверьте реестр миграций.");

		return version;
	}

	public static int GetUserVersion(SqliteConnection connection)
	{
		using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA user_version;";
		return Convert.ToInt32(command.ExecuteScalar());
	}

	public static void SetUserVersion(SqliteConnection connection, SqliteTransaction? transaction, int version)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		// PRAGMA не принимает параметры; версия приходит из кода, не из пользовательского ввода.
		command.CommandText = $"PRAGMA user_version = {version};";
		command.ExecuteNonQuery();
	}

	private static void Validate(IReadOnlyList<IDatabaseMigration> migrations)
	{
		for (int i = 0; i < migrations.Count; i++)
		{
			IDatabaseMigration migration = migrations[i];

			if (migration.ToVersion != migration.FromVersion + 1)
				throw new InvalidOperationException(
					$"Миграция {migration.FromVersion}→{migration.ToVersion} должна менять версию на единицу.");

			if (i > 0 && migration.FromVersion != migrations[i - 1].ToVersion)
				throw new InvalidOperationException(
					$"Разрыв в цепочке миграций: {migrations[i - 1].ToVersion}→{migration.FromVersion}.");
		}
	}
}
