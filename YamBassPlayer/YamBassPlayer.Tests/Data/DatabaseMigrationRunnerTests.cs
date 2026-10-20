using Microsoft.Data.Sqlite;
using YamBassPlayer.Services.Impl;
using YamBassPlayer.Services.Impl.Migrations;

namespace YamBassPlayer.Tests.Data;

[TestFixture]
public sealed class DatabaseMigrationRunnerTests
{
	private SharedMemoryDbConnectionFactory _factory = null!;

	[SetUp]
	public void SetUp()
	{
		_factory = new SharedMemoryDbConnectionFactory();
	}

	[TearDown]
	public void TearDown()
	{
		_factory.Dispose();
	}

	[Test]
	public void Initialize_CreatesBaselineAndSetsVersion5()
	{
		new DatabaseInitializer(_factory).Initialize();

		using var connection = _factory.Create();
		Assert.That(DatabaseMigrationRunner.GetUserVersion(connection), Is.EqualTo(5));
		Assert.That(SqliteSchemaHelper.HasTable(connection, "Tracks"), Is.True);
		Assert.That(SqliteSchemaHelper.HasTable(connection, "Artists"), Is.True);
	}

	[Test]
	public void Initialize_IsIdempotent()
	{
		var initializer = new DatabaseInitializer(_factory);

		Assert.DoesNotThrow(initializer.Initialize);
		Assert.DoesNotThrow(initializer.Initialize);

		using var connection = _factory.Create();
		Assert.That(DatabaseMigrationRunner.GetUserVersion(connection), Is.EqualTo(5));
	}

	[Test]
	public void Migrate_AppliesChain_FromCurrentToTarget()
	{
		using var connection = _factory.Create();
		DatabaseMigrationRunner.SetUserVersion(connection, null, 5);

		var runner = new DatabaseMigrationRunner([
			new FakeMigration(5, 6, (c, tx) => Execute(c, tx, "CREATE TABLE Step6 (Id INTEGER);")),
			new FakeMigration(6, 7, (c, tx) => Execute(c, tx, "CREATE TABLE Step7 (Id INTEGER);")),
		]);

		int version = runner.Migrate(connection, 5, 7);

		Assert.That(version, Is.EqualTo(7));
		Assert.That(DatabaseMigrationRunner.GetUserVersion(connection), Is.EqualTo(7));
		Assert.That(SqliteSchemaHelper.HasTable(connection, "Step6"), Is.True);
		Assert.That(SqliteSchemaHelper.HasTable(connection, "Step7"), Is.True);
	}

	[Test]
	public void Migrate_NoOp_WhenAlreadyAtTarget()
	{
		using var connection = _factory.Create();
		DatabaseMigrationRunner.SetUserVersion(connection, null, 5);

		var runner = new DatabaseMigrationRunner([]);

		int version = runner.Migrate(connection, 5, 5);

		Assert.That(version, Is.EqualTo(5));
	}

	[Test]
	public void Migrate_Throws_WhenDatabaseIsNewerThanApplication()
	{
		using var connection = _factory.Create();
		DatabaseMigrationRunner.SetUserVersion(connection, null, 7);

		var runner = new DatabaseMigrationRunner([]);

		Assert.Throws<InvalidOperationException>(() => runner.Migrate(connection, 7, 5));
	}

	[Test]
	public void Migrate_Throws_WhenChainHasGap()
	{
		Assert.Throws<InvalidOperationException>(() => new DatabaseMigrationRunner([
			new FakeMigration(5, 6, (_, _) => { }),
			new FakeMigration(7, 8, (_, _) => { }),
		]));
	}

	[Test]
	public void Migrate_RollsBack_WhenMigrationThrows()
	{
		using var connection = _factory.Create();
		DatabaseMigrationRunner.SetUserVersion(connection, null, 5);

		var runner = new DatabaseMigrationRunner([
			new FakeMigration(5, 6, (c, tx) =>
			{
				Execute(c, tx, "CREATE TABLE HalfApplied (Id INTEGER);");
				throw new InvalidOperationException("boom");
			}),
		]);

		Assert.Throws<InvalidOperationException>(() => runner.Migrate(connection, 5, 6));

		Assert.That(DatabaseMigrationRunner.GetUserVersion(connection), Is.EqualTo(5));
		Assert.That(SqliteSchemaHelper.HasTable(connection, "HalfApplied"), Is.False);
	}

	private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
	{
		using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}

	private sealed class FakeMigration(
		int fromVersion,
		int toVersion,
		Action<SqliteConnection, SqliteTransaction> up) : IDatabaseMigration
	{
		public int FromVersion { get; } = fromVersion;
		public int ToVersion { get; } = toVersion;
		public string Description => $"fake {FromVersion}->{ToVersion}";
		public void Up(SqliteConnection connection, SqliteTransaction transaction) => up(connection, transaction);
	}
}
