using System.Reflection;
using System.Runtime.CompilerServices;
using Moq;
using YamBassPlayer.Commands;
using YamBassPlayer.Models;
using YamBassPlayer.Plugins;
using YamBassPlayer.Services;
using YamBassPlayer.Services.Events;

namespace YamBassPlayer.Tests.Plugins;

/// <summary>
/// Плагин Яндекса собирает свои службы сам и берёт у хоста только
/// контрактные возможности — значит, его можно грузить из plugins/.
/// </summary>
[TestFixture]
public sealed class YandexPluginCompositionTests
{
    [Test]
    public void Register_AddsSourceAndMenu_WithoutHostRegistrations()
    {
        var registrar = new FakeRegistrar();

        new YandexMusicPlugin().Register(registrar);

        Assert.Multiple(() =>
        {
            Assert.That(registrar.Sources, Has.Count.EqualTo(1));
            Assert.That(registrar.Sources[0].Id.Value, Is.EqualTo("yandex"));
            Assert.That(registrar.Menus, Has.Count.EqualTo(1));
            Assert.That(registrar.TrackActions, Has.Count.EqualTo(1));
            Assert.That(registrar.Commands, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Loader_LoadsBuiltPluginFromPluginsDirectory()
    {
        string configuration = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release"
            : "Debug";

        string solutionDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string pluginsDirectory = Path.Combine(
            solutionDirectory, "YamBassPlayer", "bin", configuration, "net8.0", "plugins");

        if (!Directory.Exists(pluginsDirectory))
            Assert.Ignore("Плагин ещё не собран в plugins/");

        var registrar = new FakeRegistrar();
        var loader = new PluginLoader([], new PluginLoadReportStore(), _ => registrar);

        IReadOnlyList<PluginLoadReport> reports = loader.LoadAll(pluginsDirectory);

        PluginLoadReport report = reports.Single(r => r.PluginId == "yandex-music");
        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(PluginLoadStatus.Loaded), report.Error);
            Assert.That(registrar.Sources, Has.Count.EqualTo(1));
            Assert.That(registrar.Menus, Has.Count.EqualTo(1));
        });

        // JIT-проверка окна входа: ловит несовпадение NStack.ustring между плагином
        // и хостом (иначе конструктор Dialog «исчезает» уже в рантайме).
        Type authView = loader.LoadedPlugins.Single().GetType().Assembly
            .GetType("YamBassPlayer.Views.Impl.AuthView")!;
        ConstructorInfo constructor = authView.GetConstructor(Type.EmptyTypes)!;
        Assert.DoesNotThrow(() => RuntimeHelpers.PrepareMethod(constructor.MethodHandle));
    }

    private sealed class FakeRegistrar : IPluginRegistrar
    {
        public List<IMusicSource> Sources { get; } = [];
        public List<IMenuContributor> Menus { get; } = [];
        public List<ITrackAction> TrackActions { get; } = [];
        public List<ICommand> Commands { get; } = [];

        public IPluginStateStore State { get; } = Mock.Of<IPluginStateStore>();
        public IPluginSettings Settings { get; } = Mock.Of<IPluginSettings>();
        public IPluginLogger Log { get; } = Mock.Of<IPluginLogger>();
        public IPluginUi Ui { get; } = Mock.Of<IPluginUi>();
        public IEventBus Events { get; } = Mock.Of<IEventBus>();

        public IPluginHostServices Host { get; } = new StubHost();

        public void AddMusicSource(IMusicSource source) => Sources.Add(source);
        public void AddMenu(IMenuContributor menu) => Menus.Add(menu);
        public void AddCommand(ICommand command) => Commands.Add(command);
        public void AddTrackAction(ITrackAction action) => TrackActions.Add(action);
    }

    private sealed class StubHost : IPluginHostServices
    {
        public IErrorHandler Errors { get; } = Mock.Of<IErrorHandler>();
        public IMyWaveService MyWave { get; } = Mock.Of<IMyWaveService>();
        public ITrackInfoProvider TrackInfo { get; } = Mock.Of<ITrackInfoProvider>();
        public IPlaybackQueue Playback { get; } = Mock.Of<IPlaybackQueue>();
        public string TracksFolder => "tracks";
        public string CoversFolder => "covers";
    }
}
