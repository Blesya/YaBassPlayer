using YamBassPlayer.Services;

namespace YamBassPlayer.Services.Impl.Migrations;

/// <summary>
/// Единственный реестр миграций. Чтобы добавить шаг, достаточно положить класс
/// рядом и дописать одну строку сюда; порядок проверяется раннером на старте.
/// </summary>
internal static class DatabaseMigrationRegistry
{
	/// <summary>
	/// Собирает цепочку шагов. Состояние плагинов получает тот шаг, который переносит
	/// данные из таблицы в <c>plugin_state/&lt;id&gt;.json</c>.
	/// </summary>
	public static IReadOnlyList<IDatabaseMigration> Create(IPluginStateFile pluginState) =>
	[
		new Migration_005_To_006_CompositeTrackIdentity(),
		new Migration_006_To_007_HistoryAndCatalogs(pluginState),
		new Migration_007_To_008_LocalFavorites(pluginState),
		new Migration_008_To_009_PlaylistCompositions(),
	];
}
