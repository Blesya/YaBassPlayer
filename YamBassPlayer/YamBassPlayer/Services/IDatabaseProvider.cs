using Microsoft.Data.Sqlite;

namespace YamBassPlayer.Services;

public interface IDatabaseProvider : IDisposable
{
    SqliteConnection Connection { get; }
}