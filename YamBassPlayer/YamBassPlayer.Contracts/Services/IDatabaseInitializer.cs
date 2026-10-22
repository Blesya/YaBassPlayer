namespace YamBassPlayer.Services;

/// <summary>
/// Single owner of SQLite schema creation and migration. Runs once at startup so individual
/// services no longer create or patch tables in their constructors.
/// </summary>
public interface IDatabaseInitializer
{
	void Initialize();
}
