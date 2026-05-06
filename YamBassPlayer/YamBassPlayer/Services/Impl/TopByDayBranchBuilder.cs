using System.Threading;
using YamBassPlayer.Enums;
using YamBassPlayer.Models;

namespace YamBassPlayer.Services.Impl;

public sealed class TopByDayBranchBuilder(IHistoryService historyService) : ITreeBranchBuilder
{
    private static readonly string[] DayNames =
    [
        "Понедельник", "Вторник", "Среда", "Четверг",
        "Пятница", "Суббота", "Воскресенье"
    ];

    private static readonly DayOfWeek[] DaysOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    public int Order => TreeBranchOrder.TopByDay;
    public bool IsStatic => false;

    public Task<PlaylistTreeItem?> BuildBranchAsync(IReadOnlyList<Playlist>? existingPlaylists = null, CancellationToken ct = default)
    {
        var dayPlaylists = new List<Playlist>();
        for (int i = 0; i < DaysOrder.Length; i++)
        {
            var day = DaysOrder[i];
            var topTracks = historyService.GetTopTracksByDayOfWeek(day, 50);
            dayPlaylists.Add(new Playlist(DayNames[i], PlaylistType.TopByDay)
            {
                DayOfWeek = day,
                TrackCount = topTracks.Count
            });
        }

        return Task.FromResult<PlaylistTreeItem?>(
            PlaylistTreeItem.FromGroup(new PlaylistGroup("Топ по дням", dayPlaylists, isExpanded: false)));
    }
}
