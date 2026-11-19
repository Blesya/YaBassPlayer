using Terminal.Gui;
using YamBassPlayer.Models;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

[TestFixture]
[NonParallelizable]
public sealed class DatabaseStatisticsViewTests : ViewTestBase
{
    [Test]
    public void SetStatistics_RendersOverviewAndTabs()
    {
        var view = new DatabaseStatisticsView();
        AddToTop(view);

        view.SetStatistics(
            new DatabaseStatistics
            {
                TracksCount = 1204,
                CachedFilesCount = 12,
                CachedFilesSize = 1536,
                DatabaseFileSize = 2048
            },
            CreateListening());

        Application.Top.Redraw(Application.Top.Bounds);
        string text = RenderText(25, 80);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Обзор"));
            Assert.That(text, Does.Contain("Ритуалы"));
            Assert.That(text, Does.Contain("Вкус"));
            Assert.That(text, Does.Contain("Рекорды"));
            Assert.That(text, Does.Contain("3 ч 30 мин"));
            Assert.That(text, Does.Contain("Музыкальный возраст"));
            Assert.That(text, Does.Contain("Трек-наркотик"));
            Assert.That(text, Does.Contain("Закрыть"));
        });
    }

    [Test]
    public void SetStatistics_WithoutListens_ShowsHint()
    {
        var view = new DatabaseStatisticsView();
        AddToTop(view);

        view.SetStatistics(new DatabaseStatistics(), new ListeningStatistics());

        Application.Top.Redraw(Application.Top.Bounds);
        string text = RenderText(25, 80);

        Assert.That(text, Does.Contain("Пока нет прослушиваний"));
    }

    private static ListeningStatistics CreateListening()
    {
        int[][] heatmap = ListeningStatistics.CreateEmptyHeatmap();
        heatmap[0][23] = 4;
        heatmap[2][18] = 2;

        int[] byHour = new int[24];
        byHour[23] = 50;
        byHour[18] = 20;

        return new ListeningStatistics
        {
            TotalListens = 120,
            TotalListeningMs = 12_600_000,
            DistinctTracks = 40,
            DistinctArtists = 12,
            AverageTrackYear = 1988,
            Decades = [new StatisticsEntry("1980-е", 70)],
            ListensByHour = byHour,
            Heatmap = heatmap,
            PeakHour = 23,
            ListenerProfile = "Сова: 42% прослушиваний после полуночи",
            TopArtists = [new StatisticsEntry("Кино", 45), new StatisticsEntry("Аквариум", 12)],
            TopAlbums = [new StatisticsEntry("Группа крови", 30)],
            TopGenres = [new StatisticsEntry("рок", 90)],
            FavoriteArtistListens = 45,
            RadioListens = 60,
            LocalListens = 6,
            ActiveDays = 9,
            AverageListensPerActiveDay = 13,
            BestDay = new DateOnly(2025, 11, 2),
            BestDayListens = 47,
            CurrentStreakDays = 3,
            LongestStreakDays = 14,
            MostRepeatedTrackInDay = "Кино — Группа крови",
            MostRepeatedTrackInDayCount = 11,
            ForgottenTracks = [new ForgottenTrack("Аквариум — Поезд", 4, new DateOnly(2024, 5, 1))],
            OneTimeTracks = 17,
            FirstTrack = "Кино — Звезда",
            FirstListenDate = new DateTime(2024, 3, 12, 21, 0, 0),
            NewTracksLast30Days = 8
        };
    }
}
