namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>
/// Единственный реестр миграций. Чтобы добавить шаг, достаточно положить класс
/// рядом и дописать одну строку сюда; порядок проверяется раннером на старте.
/// </summary>
internal static class DatabaseMigrationRegistry
{
	public static IReadOnlyList<IDatabaseMigration> All { get; } =
	[
		// new Migration_005_To_006_AddSomething(),
	];
}
