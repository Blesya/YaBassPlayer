using System.Collections.Concurrent;

namespace YamBassPlayer.Services.Impl;

/// <summary>
/// Схлопывает одновременные запросы с одним ключом в одну операцию: пока первый
/// вызов выполняется, остальные ждут его результат. Нужен там, где проверка
/// «файла ещё нет» и запись файла не атомарны — иначе два потока пишут один путь.
/// </summary>
internal static class SingleFlight
{
	public static async Task<T> RunAsync<T>(
		ConcurrentDictionary<string, Lazy<Task<T>>> cache,
		string key,
		Func<Task<T>> factory)
	{
		Lazy<Task<T>> lazy = cache.GetOrAdd(
			key,
			_ => new Lazy<Task<T>>(factory, LazyThreadSafetyMode.ExecutionAndPublication));

		try
		{
			return await lazy.Value.ConfigureAwait(false);
		}
		finally
		{
			// Удаляем только свою запись: следующий вызов после завершения
			// должен снова запустить загрузку.
			cache.TryRemove(new KeyValuePair<string, Lazy<Task<T>>>(key, lazy));
		}
	}
}
