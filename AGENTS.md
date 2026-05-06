# Repository Guidelines

## Project Structure & Module Organization

The solution file is located at `YamBassPlayer/YamBassPlayer.sln`. The workspace root contains the solution directory.

```
YaBassPlayer/                         # Workspace root
└── YamBassPlayer/                    # Solution directory
    ├── YamBassPlayer.sln
    ├── YamBassPlayer/               # Source project
    │   ├── Program.cs               # Entry point (dual-init startup)
    │   ├── ServicesProvider.cs      # Autofac DI wiring (all com. registrations)
    │   ├── AutofacExtensions.cs     # Helper extensions for Autofac registration
    │   ├── Themes.cs                # TUI theme management (7 themes)
    │   ├── Services/                # 32 interfaces + Impl/ subfolder (72 files)
    │   ├── Presenters/              # 17 interfaces + Impl/ subfolder (35 files)
    │   ├── Views/                   # 9 interfaces + Impl/ subfolder (34 files)
    │   ├── Commands/                # In-app command console, split by concern: PlaybackCommands, EqualizerCommands, InfoCommands, LikeCommands, QueueCommands, SearchCommand
    │   ├── Models/                  # 14 model classes
    │   ├── Enums/                   # 7 enum files
    │   ├── Configuration/           # AppConfiguration.cs
    │   ├── Spectrum/                # ISpectrumRenderer strategy (8 renderers)
    │   ├── Constants/               # AuthConst.cs
    │   ├── Extensions/              # 4 extension classes
    │   ├── libs/                    # Native BASS DLLs (Windows, macOS, Linux)
    │   └── appsettings.example.json # Token template (real config is gitignored)
    └── YamBassPlayer.Tests/         # NUnit test project (NUnit 3.14 + Moq)
        ├── Commands/                # 3 test files
        ├── Extensions/              # 2 test files
        ├── Models/                  # 4 test files
        ├── Services/                # 9 test files
        └── Data/                    # 3 test files (SQLite integration)
```

Every service, presenter, and view follows the **interface + `Impl/` subfolder** convention. All Autofac registrations live in a single `ServiceProvider`/`ServicesProvider.cs` (`ServicesProvider.Initialise`) — there is no separate `Registration/` folder. Command classes implement `ICommand` and are registered per-class as `ICommand`.

## Build, Test, and Development Commands

Run all commands from `YamBassPlayer/` (where `.sln` lives):

```bash
dotnet build                          # Compile + copy native BASS DLLs to output
dotnet test                           # Run 290 NUnit tests (commands, extensions, models, services, SQLite)
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
- **Playlist loading**: Strategy pattern — `IPlaylistLoadStrategy` with strategies resolved by `PlaylistLoadStrategyResolver` per `PlaylistType`.
- **Command console**: `CommandInputView` (2 rows: result line + input) under `PlayStatusView`, bridged by `CommandInputPresenter` to a `CommandRegistry` (parser + resolver by name/alias). Each command implements `ICommand` and is registered per-class as `ICommand`. Commands that change playback publish typed command intents (e.g. `PlayTrackAtCommandEvent`, `PauseCommandEvent`, `SeekCommandEvent`) on the `IEventBus`; `MainWindowCoordinator` subscribes and reuses the same handlers as the UI buttons (`TogglePlayPause`, `StopPlayback`, `NextTrack`, `PlayTrackAt`…), keeping cross-cutting concerns (listen-timer, «Моя волна») in one place. Add a command by implementing `ICommand` + one DI line — no parser/UI changes. Available: `play|p [N]`, `pause|ps`, `toggle|t`, `stop|s`, `next|n`, `prev|b`, `restart|r`, `seek <0-100>`, `mode <seq|shuffle>`, `queue|q`, `eq [reset|<1-10> <0-10>]`, `search|find [ya] [-t|-ar|-alb] <текст>`, `now|track`, `clear`, `likeyandex|ly`, `likelocal|ll`, `help|?`. Non-playback command intents (e.g. `SearchCommandEvent`) are also published on the `IEventBus`; `MainWindowCoordinator` handles them by running the async work and loading the resulting transient playlist (same pipeline as the search dialogs: search → `*SearchTracks` cache → `Playlist` → `SetPlaylist` → `LoadTracksFor` → `NotifyTransientPlaylistActive`).
- **Spectrum visualization**: `ISpectrumRenderer` strategy in `Spectrum/`. 8 renderers: BarsRenderer, OscilloscopeRenderer, LissajousScopeRenderer, PolarWaveformRenderer, RingsRenderer, StereoPanScopeRenderer, Tunnel3DRenderer, WaterfallRenderer. Add a new renderer by implementing the interface and calling `_spectrum.AddRenderer()` — no enum or view changes needed.
- **Database**: SQLite (via Microsoft.Data.Sqlite + SQLitePCLRaw.bundle_e_sqlite3). Single file `tracks_cache.db`. Schema created by `HistoryService.EnsureSchema()` — no formal migration system. Write operations serialized via `IDbWriteLock`.
- **Local vs. remote detection**: `ITrackSourceDetector.IsLocal(trackId)` → `Path.IsPathRooted(trackId)`. Filepath-like IDs are local; everything else is Yandex.
- **Themes**: 7 built-in themes (Dark, Light, White, Matrix, Cyberpunk, Nord, Default) managed via `Themes.cs`.
- **Metadata**: TagLibSharp for audio file metadata reading.

## Key Dependencies

- **Autofac** (8.1.1): Dependency injection container
- **Terminal.Gui** (1.19.0): TUI framework
- **ManagedBass** (4.0.2) + **ManagedBass.Fx** (4.0.2): Audio playback
- **KM.Yandex.Music.Api** (2.0.6): Yandex.Music integration
- **Microsoft.Data.Sqlite.Core** (10.0.0) + **SQLitePCLRaw.bundle_e_sqlite3** (3.0.2): SQLite database
- **Microsoft.Extensions.Configuration** (+ JSON, Binder, 10.0.0): appsettings loading
- **TagLibSharp** (2.3.0): Audio metadata reading
- **NUnit** (3.14.0) + **Moq** (4.20.72): Testing

## Configuration & Environment

- `appsettings.json` is **gitignored**. The template at `appsettings.example.json` documents the required `YandexMusic.Token` key.
- All config reads/writes go through `AppConfiguration` (in-memory `JsonObject` cache).
- Downloaded audio is cached in `tracks/` (gitignored).
- Album covers are cached in `covers/` directory.

## Test Structure

The test project contains **259 test methods** across **19 test files**:
- **Commands/**: `CommandRegistryTests`, `CommandInputPresenterTests`, `CommandPipelineTests`
- **Extensions/**: `FileSizeExtensionsTests`, `PlaylistTypeExtensionsTests`
- **Models/**: `PlaylistTests`, `PlaylistTreeItemTests`, `TrackTests`
- **Services/**: `CoverMetadataResolverTests`, `EventBusTests`, `MusicSourceRegistryTests`, `PlaybackQueueTests`, `PlaylistLoadStrategyResolverTests`, `PlaylistTreeComposerTests`, `TrackRepositoryCacheTests`, `TrackSourceDetectorTests`
- **Data/**: `HistoryServiceTests`, `LocalFavoriteServiceTests`, `SqliteSchemaHelperTests`
