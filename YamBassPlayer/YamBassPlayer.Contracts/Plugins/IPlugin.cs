namespace YamBassPlayer.Plugins;

/// <summary>
/// Точка входа плагина. Загружается загрузчиком из <c>plugins/&lt;id&gt;/</c>
/// или регистрируется встроенно.
/// </summary>
public interface IPlugin
{
    /// <summary>Стабильный идентификатор, совпадает с именем папки и манифестом.</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>
    /// Изменилось состояние плагина: вход выполнен, папки добавлены и т. п.
    /// Хост перестраивает дерево, не дожидаясь перезапуска.
    /// </summary>
    event Action? Changed;

    /// <summary>
    /// Регистрация вкладов. Синхронно, без сети, без окон и без обращения к БД.
    /// </summary>
    void Register(IPluginRegistrar registrar);

    /// <summary>Вторая фаза, после сборки контейнера: сеть разрешена, окна — через UI-часть регистратора.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Пункт «Настройка» в меню плагина.</summary>
    Task ConfigureAsync(CancellationToken cancellationToken);
}
