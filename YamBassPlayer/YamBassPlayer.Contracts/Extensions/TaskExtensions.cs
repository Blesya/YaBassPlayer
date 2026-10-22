namespace YamBassPlayer.Extensions;

/// <summary>
/// Помощник для «запустил и забыл»-задач: наблюдает исключение, чтобы оно не потерялось
/// и не всплыло как UnobservedTaskException. Сами методы уже обрабатывают ошибки внутри.
/// </summary>
public static class TaskExtensions
{
	public static void Forget(this Task task)
		=> _ = task.ContinueWith(
			static t => _ = t.Exception,
			System.Threading.CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
}
