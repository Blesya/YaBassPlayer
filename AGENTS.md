# AGENTS.md

.NET 8 TUI music player (Terminal.Gui): Yandex.Music + local library, MVP + Autofac. UI text and commit messages are **Russian**. Four projects: `YamBassPlayer/YamBassPlayer` (app), `YamBassPlayer/YamBassPlayer.Tests` (NUnit + Moq), `YamBassPlayer/YamBassPlayer.Api` (vendored Yandex.Music API source, namespaces `YamBassPlayer.Api.*`, GPL-3.0) and `YamBassPlayer/YamBassPlayer.Api.Client` (thin client over it). `README.md` is user-facing; some of its counts/flags lag the code — trust the code and tests.

## Commands

Run from `YamBassPlayer/` (the directory with the `.sln`); the workspace root has no project or solution.

```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~PlaybackQueueTests"   # one fixture
dotnet run --project YamBassPlayer
```

- **A fresh clone fails to build with MSB3030** until `YamBassPlayer/YamBassPlayer/appsettings.json` exists: copy `appsettings.example.json` to `appsettings.json`. The file is gitignored but the csproj copies it to the output, so the build requires it.
- The full suite is fast (~4 s, 644 tests) — run all of it before finishing, not a subset.
- No CI, linter, formatter, or codegen. Existing warnings (ImageSharp NU1902/NU1903 advisories, nullable warnings, NUnit2009) are pre-existing; do not chase them.
- Do not verify changes by launching the TUI: it requires a Yandex account, takes over the terminal, and blocks. Use tests.

## Layout and conventions

- Every service/presenter/view has an interface in its folder and an implementation in `Impl/`. Follow this for new components.
- All Autofac wiring is in `ServicesProvider.Initialise` (`ServicesProvider.cs`); there is no `Registration/` folder. Most services and presenters are singletons; views are mostly transient.
- Adding a console command: implement `ICommand`, add one `builder.RegisterType<XCommand>().As<ICommand>().SingleInstance()` line. `CommandRegistry` resolves by `Name`/`Aliases`; playback commands publish typed events on `IEventBus`, handled by `MainWindowCoordinator` (same handlers as the UI buttons).
- `Program.cs` flow: config token → silent refresh → auth window if needed → `ServicesProvider.Initialise` → `Application.Init()` → BASS init → `MainWindow`. `ServicesProvider.Ioc` does not exist before auth succeeds.
- `AppConfiguration` and `Themes` are static by owner decision; keep them that way.
- `ServicesProvider.Ioc.Resolve` is acceptable here in moderation (owner decision), not an anti-pattern to "fix".
- UI strings must be Russian — no English labels, menu items, or dialogs.

## Runtime state and gotchas

- `appsettings.json` is read from `AppDomain.CurrentDomain.BaseDirectory` (the `bin/...` output), not the source tree. Editing the source copy only takes effect after a rebuild copies it.
- Runtime files live next to the executable, not in the repo: `tracks_cache.db`, `playlist_state.json`, `tracks/`, `covers/`, `logs/`.
- Logging is Serilog (`Logging.cs`), daily files `logs/yambassplayer-*.log`, Debug level. Use `Logging.LogUserAction(...)` for user-visible actions; `LogBeforeCall`/`LogAfterCall` only in rare paths, never in timers, spectrum/FFT, or progress updates (they would flood the log).
- SQLite schema and migrations are owned solely by `DatabaseInitializer.Initialize()`, called once after `Ioc` is built. The baseline is version 5; future schema changes are added as `IDatabaseMigration` steps in `Services/Impl/Migrations/DatabaseMigrationRegistry` and applied by `DatabaseMigrationRunner` using `PRAGMA user_version`. Do not create or patch tables in services; serialize writes with `IDbWriteLock`.
- Track id convention: a local track id is a rooted file path, a Yandex track id is anything else (`ITrackSourceDetector.IsLocal`).
- Auth: device-code flow is the default; QR and manual token also work. Access + refresh tokens are saved through `IAppConfiguration`; `AuthPresenter` is testable and polls through `IUiDispatcher`.
- Playlists: `IPlaylistLoadStrategy` per `PlaylistType`, resolved by `PlaylistLoadStrategyResolver`. Merged Yandex lists are deduplicated by `owner:kind` (`PlaylistDeduplicator`). Startup renders the `playlist_state.json` snapshot first (`PlaylistsPresenter.InitializeAsync`), then refreshes.
- Spectrum: 8 `ISpectrumRenderer` implementations. Add one by implementing the interface and calling `_spectrum.AddRenderer()` — no enum/view changes. Enable state, mode and frequency persist under `Spectrum:*`.
- Downloads: `ITrackDownloadTracker` is the single source of truth. The list renders `45%` while downloading, `*` when cached, `!` on failure (`TrackDownloadState.ToShortProgress`).
- Native BASS DLLs live in `libs/` and are copied by the `CopyBass` MSBuild target for build and publish; no manual step.
- `NextTrackPredictor` (Microsoft.ML) loads `next_model.zip` + `next_key_map.txt` next to the executable and degrades to "not ready" when they are absent.

## Tests

- NUnit 3.14 + Moq; the test project references the app project. No network or external services are needed.
- SQLite tests use `SharedMemoryDbConnectionFactory` (in-memory database), not the real `tracks_cache.db`.
- View/presenter tests are headless Terminal.Gui: derive from `ViewTestBase` (`FakeDriver` 80x25, `[NonParallelizable]` because `Application` state is global). Call `Pump()` after actions that post to `Application.MainLoop`, and read output with `RenderText(rows, cols)`.

## Architectural decisions (owner)

Standing decisions from the 2026-08 architecture review — not open questions:

1. `IEventBus` is the intended transport; migrate C# events onto it over time (command intents already use it).
2. Replace `"yandex"`/`"local"` literals with typed source identity (`SourceId`/`TrackId`).
3. Playback orchestration belongs in a presenter; services must not depend on presenters/UI.
4. `ServicesProvider.Ioc.Resolve` in moderation is fine.
5. Pure-decoration view interfaces may be removed.
6. Converge "id → Track" reconstructors on one owner (`ITrackCatalog` is the current step).
7. Keep `AppConfiguration` and `Themes` static.
8. Audio/presenter test coverage is backlog — do not block feature work on it.
9. Type source keys and formalize `ITreeBranchBuilder.Order` (currently magic `0/10/20`).
