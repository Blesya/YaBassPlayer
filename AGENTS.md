# Repository Guidelines

## Project Structure & Module Organization

The solution file is located at `YamBassPlayer/YamBassPlayer.sln`. The workspace root contains the solution directory.

```
YaBassPlayer/                         # Workspace root
└── YamBassPlayer/                    # Solution directory
    ├── YamBassPlayer.sln
    ├── YamBassPlayer/               # Source project
    │   ├── Program.cs               # Entry point (token from config → else auth window → main UI)
    │   ├── ServicesProvider.cs      # Autofac DI wiring (all com. registrations)
    │   ├── AutofacExtensions.cs     # Helper extensions for Autofac registration
    │   ├── Themes.cs                # TUI theme management (9 themes)
    │   ├── Services/                # 40 interfaces + Impl/ subfolder (53 files)
    │   ├── Presenters/              # 16 interfaces + Impl/ subfolder (16 files)
    │   ├── Views/                   # 18 interfaces + Impl/ subfolder (29 files)
    │   ├── Commands/                # In-app command console, split by concern: PlaybackCommands, EqualizerCommands, InfoCommands, LikeCommands, QueueCommands, SearchCommand
    │   ├── UseCases/                # Application layer (search/favorite/scan/my-wave use cases)
    │   ├── Models/                  # 15 model classes
    │   ├── Enums/                   # 9 enums (+ PlaylistTypeExtensions)
    │   ├── Configuration/           # AppConfiguration.cs + IAppConfiguration/AppConfigurationProvider
    │   ├── Spectrum/                # ISpectrumRenderer strategy (8 renderers)
    │   ├── Constants/               # AuthConst.cs
    │   ├── Extensions/              # 7 extension classes
    │   ├── libs/                    # Native BASS DLLs (Windows, macOS, Linux)
    │   └── appsettings.example.json # Token template (real config is gitignored)
    └── YamBassPlayer.Tests/         # NUnit test project (NUnit 3.14 + Moq)
        ├── Commands/                # 3 test files
        ├── Data/                    # 4 test files (SQLite integration)
        ├── Extensions/              # 3 test files
        ├── Models/                  # 3 test files
        ├── Presenters/              # 16 test files
        ├── Services/                # 15 test files
        ├── UseCases/                # 4 test files
        └── Views/                   # 9 test files (headless Terminal.Gui via FakeDriver)
```

Every service, presenter, and view follows the **interface + `Impl/` subfolder** convention. All Autofac registrations live in a single `ServiceProvider`/`ServicesProvider.cs` (`ServicesProvider.Initialise`) — there is no separate `Registration/` folder. Command classes implement `ICommand` and are registered per-class as `ICommand`.

## Build, Test, and Development Commands

Run all commands from `YamBassPlayer/` (where `.sln` lives):

```bash
dotnet build                          # Compile + copy native BASS DLLs to output
dotnet test                           # Run 579 NUnit tests (commands, extensions, models, services, presenters, views, SQLite)
dotnet run --project YamBassPlayer    # Launch the TUI
```

All commands run from where the `.sln` file lives. Before committing, run both `dotnet build` and `dotnet test` to ensure nothing is broken.

## Coding Style & Naming Conventions

- **Language**: C# with standard .NET conventions (.NET 8.0).
- **Interface prefix**: `I` (e.g., `IAudioPlayer`, `IPlaylistTreeComposer`).
- **Implementation placement**: Always inside an `Impl/` subfolder under the interface's directory.
- **DI registration**: All services registered as singletons in `ServicesProvider.cs` via Autofac. There is no separate `Registration/` folder.
- **UI strings**: The entire UI is in **Russian**. Do not introduce English labels, menu items, or dialog text.
- **No formatter/linter** is enforced — keep code consistent with the surrounding style.

## Commit & Pull Request Guidelines

- Commit messages use **Russian**, are short, and describe the change (e.g., `Фикс поиска`, `Локальная музыка`).
- Keep commits focused — a single logical change per commit.
- No formal PR template exists. Include a brief description of what changed and why.
- Before submitting, verify `dotnet build` and `dotnet test` both pass clean.

## Architecture Overview

- **Pattern**: MVP (Model-View-Presenter) with Autofac DI. All services registered as singletons in `ServicesProvider.cs`.
- **UI Framework**: Terminal.Gui (v1.19.0) for TUI.
- **Audio**: ManagedBass via `IAudioPlayer` — play, pause, stop, seek, 10-band equalizer, FFT. Native BASS libraries for Windows (bass.dll, bass_fx.dll), macOS (libbass.dylib, libbass_fx.dylib), and Linux (libbass.so).
- **Music sources**: `IMusicSource` for Yandex.Music and local files, auto-registered via `IMusicSourceRegistry`.
- **Auth (Yandex.Music)**: `IAuthService`/`AuthService` wraps the library's `YandexMusicApi`. `Program.cs` first tries the token from `appsettings.json`; if it is missing or rejected it silently renews it with the saved refresh token (`TryRefreshAsync`), and only then opens `AuthView` (full-screen `Dialog`) driven by `AuthPresenter`, offering **device-code login**, **QR login** or **token** entry. Device code (OAuth Device Flow, the default): `StartDeviceAuthorizationAsync()` asks `oauth.yandex.ru/device/code` through `DeviceAuthorizationService` (`IDeviceAuthorizationService`, plain `HttpClient`), the view shows the short `user_code` and `https://ya.ru/device` (the code is copied to the OS clipboard through `Terminal.Gui.Clipboard`, the «Копировать код» button repeats it), and the presenter polls `CheckDeviceAuthorizationAsync()` every `interval` seconds (5 min lifetime) until confirmed; the result carries `access_token` + `refresh_token`, both saved through `IAppConfiguration`. QR: `StartQrAuthorizationAsync()` opens a library session and returns a link, `QrCodeRenderer` (`IQrCodeRenderer`) renders it as text using half-block characters via `Net.Codecrete.QrCodeGenerator`, and the presenter polls `CheckQrAuthorizationAsync()` every 2s (5 min lifetime) until confirmed. `IAppConfiguration` and `IErrorHandler` are injected so the presenter is unit-testable; polling uses `IUiDispatcher`.
- **Playlist loading**: Strategy pattern — `IPlaylistLoadStrategy` with strategies resolved by `PlaylistLoadStrategyResolver` per `PlaylistType`. Yandex playlists come from three places: «Мои треки» (library likes), landing mixes (`GetPersonalPlaylistsAsync`, type `Custom`) and the user's own playlists (`/users/{uid}/playlists/list` — the library has no method, so it goes through its request provider; type `YandexOwnPlaylist`) plus playlists saved from others (`GetLikedPlaylistsAsync`, type `YandexLikedPlaylist`). Own playlists are grouped under «Мои плейлисты» in the tree, everything else stays in the source root. The same playlist can come from several endpoints (e.g. «Плейлист дня» is both a landing mix and an entry in the user's list), so the merged list is deduplicated by `owner:kind` (`PlaylistDeduplicator`).
- **Fast startup**: `IPlaylistStateStore` persists a JSON snapshot (`playlist_state.json`, gitignored) of the last run's playlist list, favourite/custom track IDs and last selected playlist. `PlaylistsPresenter.InitializeAsync` renders that snapshot instantly, then `RefreshAsync` re-fetches from the sources and re-renders (the splash is removed on the first `PlaylistChosen`). `ITrackRepository.GetPersistedSnapshot()` primes the in-memory track-id caches from the snapshot so the restored playlist draws without network access.
- **Command console**: `CommandInputView` (2 rows: result line + input) under `PlayStatusView`, bridged by `CommandInputPresenter` to a `CommandRegistry` (parser + resolver by name/alias). Each command implements `ICommand` and is registered per-class as `ICommand`. Commands that change playback publish typed command intents (e.g. `PlayTrackAtCommandEvent`, `PauseCommandEvent`, `SeekCommandEvent`) on the `IEventBus`; `MainWindowCoordinator` subscribes and reuses the same handlers as the UI buttons (`TogglePlayPause`, `StopPlayback`, `NextTrack`, `PlayTrackAt`…), keeping cross-cutting concerns (listen-timer, «Моя волна») in one place. Add a command by implementing `ICommand` + one DI line — no parser/UI changes. Available: `play|p [N]`, `pause|ps`, `toggle|t`, `stop|s`, `next|n`, `prev|b`, `restart|r`, `seek <0-100>`, `mode <seq|shuffle|repeat>`, `queue|q`, `eq [reset|<1-10> <0-10>]`, `volume|vol [0-100|+N|-N]`, `search|find [ya] [-t|-ar|-alb] <текст>`, `now|track`, `clear`, `likeyandex|ly`, `likelocal|ll`, `help|?`. Non-playback command intents (e.g. `SearchCommandEvent`) are also published on the `IEventBus`; `MainWindowCoordinator` handles them by running the async work and loading the resulting transient playlist (same pipeline as the search dialogs: search → `*SearchTracks` cache → `Playlist` → `SetPlaylist` → `LoadTracksFor` → `NotifyTransientPlaylistActive`).
- **Spectrum visualization**: `ISpectrumRenderer` strategy in `Spectrum/`. 8 renderers: BarsRenderer, OscilloscopeRenderer, LissajousScopeRenderer, PolarWaveformRenderer, RingsRenderer, StereoPanScopeRenderer, Tunnel3DRenderer, WaterfallRenderer. Add a new renderer by implementing the interface and calling `_spectrum.AddRenderer()` — no enum or view changes needed. The «Спектр: вкл/выкл» button next to the mode/frequency buttons hides the spectrum (its render timer is skipped) and gives the freed rows back to the track list; the state persists in `appsettings.json` (`Spectrum:Enabled`).
- **Database**: SQLite (via Microsoft.Data.Sqlite + SQLitePCLRaw.bundle_e_sqlite3). Single file `tracks_cache.db`. Schema creation and migration live in one place — `IDatabaseInitializer`/`DatabaseInitializer.Initialize()`, called once at startup after `Ioc = builder.Build()` (`PRAGMA user_version = 5`); individual services no longer create or patch tables. Write operations serialized via `IDbWriteLock`.
- **Local vs. remote detection**: `ITrackSourceDetector.IsLocal(trackId)` → `Path.IsPathRooted(trackId)`. Filepath-like IDs are local; everything else is Yandex.
- **Download progress**: `ITrackDownloadTracker` (`Services/Impl/TrackDownloadTracker.cs`) is the single source of truth for in-flight downloads. `TrackFileProvider` reports start/progress/outcome there, `TracksPresenter` subscribes and pushes per-track state into `ITrackListModel.SetDownloadState`, and the list renders the state next to the track number: `45%` while downloading, `*` when cached, `!` on failure. Nothing else needs to poll the file system to know a track just got cached.
- **Themes**: 9 built-in themes (Dark, Light, White, Matrix, Cyberpunk, Nord, Amber, Mint, Default) managed via `Themes.cs`.
- **Metadata**: TagLibSharp for audio file metadata reading.

## Key Dependencies

- **Autofac** (8.1.1): Dependency injection container
- **Terminal.Gui** (1.19.0): TUI framework
- **ManagedBass** (4.0.2) + **ManagedBass.Fx** (4.0.2): Audio playback
- **KM.Yandex.Music.Api** (2.0.6): Yandex.Music integration
- **Microsoft.Data.Sqlite.Core** (10.0.0) + **SQLitePCLRaw.bundle_e_sqlite3** (3.0.2): SQLite database
- **Microsoft.Extensions.Configuration** (+ JSON, Binder, 10.0.0): appsettings loading
- **TagLibSharp** (2.3.0): Audio metadata reading
- **SixLabors.ImageSharp** (3.1.12): Cover decoding for the ASCII renderer (declared in `Directory.Build.props`)
- **Net.Codecrete.QrCodeGenerator** (3.2.1): QR-code generation for the QR login screen
- **NUnit** (3.14.0) + **Moq** (4.20.72): Testing

## Configuration & Environment

- `appsettings.json` is **gitignored**. The template at `appsettings.example.json` documents the `YandexMusic.Token` key (written automatically after a successful login; can also be set by hand).
- All config reads/writes go through `AppConfiguration` (in-memory `JsonObject` cache), injected into presenters through the `IAppConfiguration` wrapper.
- Downloaded audio is cached in `tracks/` (gitignored).
- Album covers are cached in `covers/` directory.

## Test Structure

The test project contains **579 tests** across **56 fixture files** (helpers like `ViewTestBase`, `FakeUiDispatcher`, `SharedMemoryDbConnectionFactory` are not fixtures):
- **Commands/**: `CommandRegistryTests`, `CommandInputPresenterTests`, `CommandPipelineTests`
- **Data/** (SQLite integration): `HistoryServiceTests`, `LocalFavoriteServiceTests`, `SqliteSchemaHelperTests`
- **Extensions/**: `FileSizeExtensionsTests`, `PlaylistTypeExtensionsTests`, `DurationExtensionsTests`
- **Models/**: `PlaylistTests`, `PlaylistTreeItemTests`, `TrackTests`
- **Presenters/**: `PlaybackPresenterTests`, `TracksPresenterTests`, `PlaylistsPresenterTests`, `TrackInfoPanelPresenterTests`, `PlayStatusPresenterTests`, `MyWavePresenterTests`, `MyWaveWindowPresenterTests`, `NowPlayingPresenterTests`, `LocalSearchPresenterTests`, `YandexSearchPresenterTests`, `LocalFolderManagerPresenterTests`, `EqualizerPresenterTests`, `DatabaseStatisticsPresenterTests`, `LargeTrackInfoPresenterTests`, `AuthPresenterTests`
- **Services/**: `CoverMetadataResolverTests`, `EventBusTests`, `MusicSourceRegistryTests`, `PlaybackQueueTests`, `PlaylistLoadStrategyResolverTests`, `PlaylistTreeComposerTests`, `TrackRepositoryCacheTests`, `TrackSourceDetectorTests`, `TrackCatalogTests`, `TrackInfoProviderTests`, `PlaylistStateStoreTests`, `LocalMusicSourceTests`, `LocalAlbumLoadStrategyTests`, `QrCodeRendererTests`, `DeviceAuthorizationServiceTests`
- **UseCases/**: `SearchAndLoadPlaylistUseCaseTests`, `ToggleFavoriteUseCaseTests`, `ScanLibraryUseCaseTests`, `ShowMyWaveUseCaseTests`
- **Views/** (headless Terminal.Gui via `FakeDriver`, `[NonParallelizable]`): `TracksViewTests`, `TracksTileViewTests`, `TracksViewHostTests`, `PlaylistRowsViewTests`, `PlaylistsViewTests`, `PlayStatusViewTests`, `SpectrumRendererTests`, `SeekBarViewTests`, `ViewFactoryTests`

## Architectural Decisions (owner)

Recorded from the 2026-08 architecture review — these are standing decisions, not open questions:

1. **`IEventBus` is the intended transport**, not dead code: C# events should migrate onto it over time (already used for command intents).
2. **Source identity becomes typed**: replace the `"yandex"`/`"local"` literals spread across models, SQL, DI keys and presenters with a typed value object (`SourceId`/`TrackId`).
3. **Playback orchestration belongs in a presenter**: a service (e.g. `PlaybackCoordinator`) must not depend on presenters/UI.
4. **`ServicesProvider.Ioc.Resolve` is acceptable in moderation** — the owner does not consider it an anti-pattern here; full constructor-injection everywhere is not required.
5. **View interfaces may be removed** when they are pure decoration; they can be reintroduced later if a second implementation is ever needed.
6. **One owner for "id → Track"**: converge the independent reconstructors (`TrackInfoProvider`, `LocalLibraryService`, `LocalMusicSource`) on a single owner (`ITrackCatalog` is the current step).
7. **Keep `AppConfiguration` and `Themes` static** — the owner wants it that way; do not convert them to DI/data-driven strategies.
8. **Tests for the audio layer and presenters are backlog, not a priority** — do not block feature work on coverage.
9. **Typed source keys and a formal `Order`** for `ITreeBranchBuilder` (currently magic `0/10/20`) — replace literals and formalize ordering.
