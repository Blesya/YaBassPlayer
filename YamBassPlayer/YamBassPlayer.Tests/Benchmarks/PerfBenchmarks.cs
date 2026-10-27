using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Terminal.Gui;
using YamBassPlayer.Extensions;
using YamBassPlayer.Models;
using YamBassPlayer.Spectrum;
using YamBassPlayer.Views;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Benchmarks;

/// <summary>
/// Замеры горячих путей. Не входят в обычный прогон (<see cref="ExplicitAttribute"/>):
/// запускаются вручную <c>dotnet test --filter "FullyQualifiedName~PerfBenchmarks"</c>.
/// </summary>
/// <remarks>
/// <para>
/// Мерим то, что происходит внутри процесса: сборку кадра, объём вывода, аллокации.
/// Стоимость самого терминала (разбор escape-последовательностей, ConPTY) из процесса
/// не видна.
/// </para>
/// <para>
/// Кадровый путь меряется копиями обоих алгоритмов, а не самим <c>NetDriver</c>:
/// <c>UpdateScreen</c> внутри проверяет <c>Console.WindowHeight</c>, а testhost
/// запускается без консольного хэндла и это бросает IOException. Копии читают те же
/// буферы (клетка/атрибут/24-битный цвет) и пишут в один и тот же приёмник.
/// Продакшн-класс в настоящей консоли (120×30, обложка 34×17): 0,221 мс и 1 запись
/// в консоль против 2,232 мс, 37 записей и 593 КБ аллокаций у прежнего кода.
/// </para>
/// </remarks>
[TestFixture]
[NonParallelizable]
[Explicit ("Замеры производительности: запускаются вручную")]
public sealed class PerfBenchmarks
{
    private const int Iterations = 200;
    private const int Width = 120;
    private const int Height = 30;

    // ── Кадр драйвера: старый и новый алгоритм на одном и том же экране ────

    [Test]
    public void DriverFrame_FullRepaint_NewVsLegacy()
    {
        var (contents, rgb, dirty) = CreatePlayerScreen(Height, Width);
        var writer = new CountingWriter();

        var current = Measure(Iterations, () =>
        {
            MarkAllDirty(contents, dirty, Height, Width);
            CurrentUpdateScreen(contents, rgb, dirty, Height, Width, writer);
        }, writer);

        var legacy = Measure(Iterations, () =>
        {
            MarkAllDirty(contents, dirty, Height, Width);
            LegacyUpdateScreen(contents, rgb, dirty, Height, Width, writer);
        }, writer);

        var idleCurrent = Measure(Iterations, () =>
            CurrentUpdateScreen(contents, rgb, dirty, Height, Width, writer), writer);
        var idleLegacy = Measure(Iterations, () =>
            LegacyUpdateScreen(contents, rgb, dirty, Height, Width, writer), writer);

        TestContext.Out.WriteLine($"кадр экрана {Width}×{Height} (с обложкой 34×17 в TrueColor):");
        Report("полная перерисовка: новый", current, legacy);
        Report("холостой кадр: новый", idleCurrent, idleLegacy);

        Assert.Multiple(() =>
        {
            Assert.That(current.AllocatedBytes, Is.LessThan(legacy.AllocatedBytes / 4),
                "новый кадр не должен аллоцировать сотни килобайт");
            Assert.That(current.Writes, Is.LessThan(legacy.Writes / 4),
                "кадр должен уходить в консоль одной-двумя записями");
        });
    }

    // ── Спектр: сколько клеток уходит в драйвер за кадр ───────────────────

    [Test]
    public void SpectrumFrame_CellsSentPerFrame()
    {
        var driver = new FakeDriver();
        Application.Init(driver, null!);
        try
        {
            const int width = 120;
            const int height = 10;
            var host = new View { Width = width, Height = height };
            Application.Top.Add(host);
            Application.Top.LayoutSubviews();

            var canvas = new SpectrumCanvas();
            var renderer = new BarsRenderer();
            var random = new Random(42);
            var data = new float[128];

            long cells = 0;
            const int frames = 300;
            var stopwatch = Stopwatch.StartNew();
            for (int frame = 0; frame < frames; frame++)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = Math.Abs(MathF.Sin((frame + i) * 0.15f)) * (float)random.NextDouble();
                }

                canvas.BeginFrame(width, height, new Terminal.Gui.Attribute(Color.White, Color.Black));
                renderer.Render(new Rect(0, 0, width, height), canvas, data, 22050);
                cells += canvas.Flush(host, driver, full: frame == 0);
            }

            double perFrameMs = stopwatch.Elapsed.TotalMilliseconds / frames;

            TestContext.Out.WriteLine($"спектр {width}×{height}, режим FFT, {frames} кадров:");
            TestContext.Out.WriteLine($"  клеток в драйвер за кадр: {cells / (double)frames:F1} " +
                $"(было: полная заливка полосы = {width * height} клеток плюс перерисовка всего экрана)");
            TestContext.Out.WriteLine($"  время кадра (рендер + разница): {perFrameMs:F3} мс");

            Assert.That(cells / (double)frames, Is.LessThan(width * height / 2.0));
        }
        finally
        {
            Application.Shutdown();
        }
    }

    // ── Ширина колонки времени: старый и новый расчёт на 5000 треков ──────

    [Test]
    public void DurationColumnWidth_AllocationsOn5000Tracks()
    {
        const int count = 5000;
        var items = new TrackListItem[count];
        for (int i = 0; i < count; i++)
        {
            var track = new Track("T", "A", "Al", i.ToString())
            {
                SourceId = new SourceId("yandex"),
                DurationMs = 60_000 + i * 37
            };
            items[i] = new TrackListItem(track, i.ToString(), TrackDownloadState.Idle);
        }

        var legacy = Measure(20, () => LegacyDurationWidth(items));
        var current = Measure(20, () => ShortDurationWidth(items));

        TestContext.Out.WriteLine($"ширина колонки времени на {count} треках:");
        Report("новый", current, legacy);

        Assert.That(current.AllocatedBytes, Is.LessThan(legacy.AllocatedBytes));
    }

    // ── Снимок плейлистов: из чего складывается синхронный Save ───────────

    [Test]
    public void PlaylistStateSave_Cost()
    {
        string file = Path.Combine(Path.GetTempPath(), "ybp-bench-" + Guid.NewGuid().ToString("N") + ".json");
        string stateDir = Path.Combine(Path.GetTempPath(), "ybp-bench-state-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new YamBassPlayer.Services.Impl.PlaylistStateStore(
                file, new YamBassPlayer.Services.Impl.PluginStateFile(stateDir));

            var playlists = new List<Playlist>();
            for (int i = 0; i < 200; i++)
            {
                playlists.Add(new Playlist("playlist", $"Плейлист {i}")
                {
                    SourceId = new SourceId("yandex"),
                    Category = YamBassPlayer.Enums.PlaylistCategory.Data,
                    Description = "Описание плейлиста для замера стоимости записи снимка",
                    TrackCount = 100
                });
            }

            var state = new PlaylistState { Playlists = playlists };
            store.Save(state);
            string json = File.ReadAllText(file);

            var total = Measure(20, () => store.Save(state));
            var writeOnly = Measure(20, () => File.WriteAllText(file, json));

            TestContext.Out.WriteLine($"PlaylistState.Save (200 плейлистов, {json.Length / 1024.0:F0} КБ JSON):");
            TestContext.Out.WriteLine($"  всего: {total.PerCallMs:F2} мс, " +
                $"{total.AllocatedBytes / Iterations / 1024.0:F1} КБ аллокаций");
            TestContext.Out.WriteLine($"  из них только запись файла: {writeOnly.PerCallMs:F2} мс " +
                "(остальное — сериализация)");
        }
        finally
        {
            if (File.Exists(file))
                File.Delete(file);
            if (Directory.Exists(stateDir))
                Directory.Delete(stateDir, true);
        }
    }

    // ── Конфигурация: во что обходился пересбор конфига на каждую запись ──

    [Test]
    public void ConfigurationReload_Cost()
    {
        string directory = Path.GetTempPath();
        var result = Measure(20, () =>
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
            _ = configuration["Ui:TrueColor"];
        });

        TestContext.Out.WriteLine("пересбор IConfiguration с reloadOnChange (как было на каждой записи " +
            $"конфига): {result.PerCallMs:F2} мс, {result.AllocatedBytes / Iterations / 1024.0:F1} КБ аллокаций");
    }

    // ── Инфраструктура ───────────────────────────────────────────────────

    private readonly record struct Measurement(double PerCallMs, long AllocatedBytes, int Writes, long Chars);

    private static Measurement Measure(int iterations, Action action, CountingWriter? counter = null)
    {
        action();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        long before = GC.GetAllocatedBytesForCurrentThread();
        int writesBefore = counter?.WriteCalls ?? 0;
        long charsBefore = counter?.Chars ?? 0;

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }

        stopwatch.Stop();
        return new Measurement(
            stopwatch.Elapsed.TotalMilliseconds / iterations,
            GC.GetAllocatedBytesForCurrentThread() - before,
            (counter?.WriteCalls ?? 0) - writesBefore,
            (counter?.Chars ?? 0) - charsBefore);
    }

    private static void Report(string name, Measurement current, Measurement legacy)
    {
        TestContext.Out.WriteLine($"  {name,-26} {current.PerCallMs,7:F3} мс  " +
            $"{current.AllocatedBytes / (double)Iterations / 1024.0,7:F1} КБ  " +
            $"{current.Writes / (double)Iterations,6:F1} записей  " +
            $"{current.Chars / (double)Iterations,8:F0} символов");
        TestContext.Out.WriteLine($"  {"было",-26} {legacy.PerCallMs,7:F3} мс  " +
            $"{legacy.AllocatedBytes / (double)Iterations / 1024.0,7:F1} КБ  " +
            $"{legacy.Writes / (double)Iterations,6:F1} записей  " +
            $"{legacy.Chars / (double)Iterations,8:F0} символов");
    }

    /// <summary>Экран, похожий на настоящий: рамки, список, дерево, обложка, полоса спектра.</summary>
    private static (int[,,] Contents, (bool Has, byte FR, byte FG, byte FB, byte BR, byte BG, byte BB)[,] Rgb, bool[] Dirty)
        CreatePlayerScreen(int rows, int cols)
    {
        var contents = new int[rows, cols, 3];
        var rgb = new (bool, byte, byte, byte, byte, byte, byte)[rows, cols];
        var dirty = new bool[rows];

        int normal = Attr(ConsoleColor.White, ConsoleColor.Black);
        int dim = Attr(ConsoleColor.DarkGray, ConsoleColor.Black);
        int hot = Attr(ConsoleColor.Cyan, ConsoleColor.Black);
        int focus = Attr(ConsoleColor.Black, ConsoleColor.Gray);

        for (int row = 0; row < rows; row++)
        {
            dirty[row] = true;
            for (int col = 0; col < cols; col++)
            {
                int attr = row < 2 || row >= rows - 9
                    ? (row == 0 ? focus : dim)
                    : col < 30
                        ? (col % 2 == 0 ? dim : normal)
                        : (col % 17 == 0 ? hot : normal);

                contents[row, col, 0] = 'a' + (row * cols + col) % 26;
                contents[row, col, 1] = attr;
                contents[row, col, 2] = 1;
            }
        }

        // Обложка в панели «Инфо»: 34×17 клеток, у каждой свой 24-битный цвет.
        int coverX = cols - 36;
        int coverY = 3;
        for (int y = 0; y < 17 && coverY + y < rows; y++)
        {
            for (int x = 0; x < 34 && coverX + x < cols; x++)
            {
                rgb[coverY + y, coverX + x] = (true,
                    (byte)(x * 7), (byte)(y * 13), 90,
                    (byte)(y * 5), (byte)(x * 3), 40);
            }
        }

        return (contents, rgb, dirty);
    }

    /// <summary>Кодировка атрибута та же, что в NetDriver.MakeColor: fg в старших 16 битах, bg в младших.</summary>
    private static int Attr(ConsoleColor foreground, ConsoleColor background)
        => (((int)foreground & 0xffff) << 16) | ((int)background & 0xffff);

    private static void MarkAllDirty(int[,,] contents, bool[] dirty, int rows, int cols)
    {
        for (int row = 0; row < rows; row++)
        {
            dirty[row] = true;
            for (int col = 0; col < cols; col++)
            {
                contents[row, col, 2] = 1;
            }
        }
    }

    // ── Копия текущего алгоритма кадра (NetDriver.UpdateScreen) ───────────

    private static void CurrentUpdateScreen(
        int[,,] contents,
        (bool Has, byte FR, byte FG, byte FB, byte BR, byte BG, byte BB)[,] rgb,
        bool[] dirty,
        int rows,
        int cols,
        CountingWriter writer)
    {
        var frame = new char[16 * 1024];
        int length = 0;
        var cache = new Dictionary<int, string>();
        int redrawAttr = -1;
        (bool, byte, byte, byte, byte, byte, byte) redrawRgb = default;
        bool redrawRgbSet = false;
        bool wroteCell = false;

        void AppendText(string s)
        {
            if (length + s.Length > frame.Length)
                Array.Resize(ref frame, Math.Max(frame.Length * 2, length + s.Length));
            s.CopyTo(0, frame, length, s.Length);
            length += s.Length;
        }

        void AppendInt(int value)
        {
            if (value >= 10)
                AppendInt(value / 10);
            AppendChar((char)('0' + value % 10));
        }

        void AppendChar(char c)
        {
            if (length == frame.Length)
                Array.Resize(ref frame, frame.Length * 2);
            frame[length++] = c;
        }

        void AppendCursor(int col, int row)
        {
            AppendText("\x1b[");
            AppendInt(row + 1);
            AppendChar(';');
            AppendInt(col + 1);
            AppendChar('H');
        }

        string AttributeSequence(int attr)
        {
            if (cache.TryGetValue(attr, out string? cached))
                return cached;
            string sequence = $"\x1b[{LegacyMapColors((ConsoleColor)(attr & 0xffff), false)};" +
                $"{LegacyMapColors((ConsoleColor)((attr >> 16) & 0xffff))}m";
            cache[attr] = sequence;
            return sequence;
        }

        for (int row = 0; row < rows; row++)
        {
            if (!dirty[row])
                continue;
            dirty[row] = false;

            int nextCol = -1;
            for (int col = 0; col < cols; col++)
            {
                if (contents[row, col, 2] == 0)
                    continue;

                if (!wroteCell)
                {
                    wroteCell = true;
                    AppendText("\x1b[?25l");
                }

                if (nextCol != col)
                    AppendCursor(col, row);

                int attr = contents[row, col, 1];
                var cell = rgb[row, col];
                if (cell.Has)
                {
                    if (!redrawRgbSet || redrawRgb != cell)
                    {
                        AppendText("\x1b[38;2;");
                        AppendInt(cell.FR); AppendChar(';'); AppendInt(cell.FG); AppendChar(';'); AppendInt(cell.FB);
                        AppendText(";48;2;");
                        AppendInt(cell.BR); AppendChar(';'); AppendInt(cell.BG); AppendChar(';'); AppendInt(cell.BB);
                        AppendChar('m');
                        redrawRgb = cell;
                        redrawRgbSet = true;
                        redrawAttr = -1;
                    }
                }
                else if (attr != redrawAttr)
                {
                    AppendText(AttributeSequence(attr));
                    redrawAttr = attr;
                    redrawRgbSet = false;
                }

                AppendChar((char)contents[row, col, 0]);
                contents[row, col, 2] = 0;
                nextCol = col + 1;
            }
        }

        if (!wroteCell)
            return;

        AppendCursor(0, 0);
        writer.Write(frame, 0, length);
    }

    // ── Копия прежнего алгоритма кадра (для сравнения) ───────────────────

    private static void LegacyUpdateScreen(
        int[,,] contents,
        (bool Has, byte FR, byte FG, byte FB, byte BR, byte BG, byte BB)[,] rgb,
        bool[] dirty,
        int rows,
        int cols,
        CountingWriter writer)
    {
        int redrawAttr = -1;
        var output = new StringBuilder();

        for (int row = 0; row < rows; row++)
        {
            if (!dirty[row])
                continue;

            dirty[row] = false;
            output.Clear();
            int lastCol = -1;
            for (int col = 0; col < cols; col++)
            {
                lastCol = -1;
                int outputWidth = 0;
                for (; col < cols; col++)
                {
                    if (contents[row, col, 2] == 0)
                    {
                        if (output.Length > 0)
                        {
                            writer.Write(output);
                            output.Clear();
                            lastCol += outputWidth;
                            outputWidth = 0;
                        }
                        else if (lastCol == -1)
                        {
                            lastCol = col;
                        }

                        if (lastCol + 1 < cols)
                            lastCol++;
                        continue;
                    }

                    if (lastCol == -1)
                        lastCol = col;

                    int attr = contents[row, col, 1];
                    var cell = rgb[row, col];
                    if (cell.Has)
                    {
                        redrawAttr = -1;
                        AppendRgb(output, cell.FR, cell.FG, cell.FB, true);
                        AppendRgb(output, cell.BR, cell.BG, cell.BB, false);
                    }
                    else if (attr != redrawAttr)
                    {
                        redrawAttr = attr;
                        output.Append(LegacyWriteAttributes(attr));
                    }

                    outputWidth++;
                    output.Append((char)contents[row, col, 0]);
                    contents[row, col, 2] = 0;
                }
            }

            if (output.Length > 0)
                writer.Write(output);
        }
    }

    private static void AppendRgb(StringBuilder sb, byte r, byte g, byte b, bool foreground)
    {
        sb.Append(foreground ? "\x1b[38;2;" : "\x1b[48;2;")
            .Append(r).Append(';').Append(g).Append(';').Append(b).Append('m');
    }

    private static StringBuilder LegacyWriteAttributes(int attr)
    {
        const string CSI = "\x1b[";
        int bg = 0;
        int fg = 0;
        var sb = new StringBuilder();

        IEnumerable<int> values = Enum.GetValues(typeof(ConsoleColor))
            .OfType<ConsoleColor>()
            .Select(s => (int)s);
        if (values.Contains(attr & 0xffff))
            bg = LegacyMapColors((ConsoleColor)(attr & 0xffff), false);
        if (values.Contains((attr >> 16) & 0xffff))
            fg = LegacyMapColors((ConsoleColor)((attr >> 16) & 0xffff));

        sb.Append($"{CSI}{bg};{fg}m");
        return sb;
    }

    private static int LegacyMapColors(ConsoleColor color, bool isForeground = true)
        => ((int)color) switch
        {
            0 => isForeground ? 30 : 40,
            1 => isForeground ? 34 : 44,
            2 => isForeground ? 32 : 42,
            3 => isForeground ? 36 : 46,
            4 => isForeground ? 31 : 41,
            5 => isForeground ? 35 : 45,
            6 => isForeground ? 33 : 43,
            7 => isForeground ? 37 : 47,
            8 => isForeground ? 90 : 100,
            9 => isForeground ? 94 : 104,
            10 => isForeground ? 92 : 102,
            11 => isForeground ? 96 : 106,
            12 => isForeground ? 91 : 101,
            13 => isForeground ? 95 : 105,
            14 => isForeground ? 93 : 103,
            _ => isForeground ? 97 : 107
        };

    // ── Копия старого расчёта ширины колонки времени ─────────────────────

    private static int LegacyDurationWidth(TrackListItem[] items)
    {
        int max = 0;
        foreach (TrackListItem item in items)
        {
            int length = item.Track.DurationMs.ToShortDuration().Length;
            if (length > max)
                max = length;
        }

        return max == 0 ? 0 : max + 1;
    }

    private static int ShortDurationWidth(TrackListItem[] items)
    {
        int max = 0;
        foreach (TrackListItem item in items)
        {
            int length = ShortDurationLength(item.Track.DurationMs);
            if (length > max)
                max = length;
        }

        return max == 0 ? 0 : max + 1;
    }

    private static int ShortDurationLength(long? milliseconds)
    {
        if (milliseconds is not { } ms || ms <= 0)
            return 0;

        double minutes = ms / 60000.0;
        if (minutes >= 60)
            return minutes >= 600 ? 8 : 7;

        return minutes >= 10 ? 5 : 4;
    }

    /// <summary>Приёмник вывода: считает вызовы и символы, ничего не пишет.</summary>
    private sealed class CountingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public int WriteCalls { get; private set; }

        public long Chars { get; private set; }

        public override void Write(char value)
        {
            WriteCalls++;
            Chars++;
        }

        public override void Write(char[] buffer, int index, int count)
        {
            WriteCalls++;
            Chars += count;
        }

        public override void Write(string? value)
        {
            WriteCalls++;
            Chars += value?.Length ?? 0;
        }
    }
}
