using Moq;
using YamBassPlayer.Models;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Services;
using YamBassPlayer.Views;

namespace YamBassPlayer.Tests.Presenters;

[TestFixture]
public sealed class DatabaseStatisticsPresenterTests
{
    [Test]
    public void ShowStatisticsDialog_CollectsAndDisplaysStatistics()
    {
        var stats = new DatabaseStatistics { TracksCount = 42, TotalListens = 7 };
        var service = new Mock<IDatabaseStatisticsService>();
        service.Setup(s => s.CollectStatistics()).Returns(stats);

        var viewFactory = new Mock<IViewFactory>();
        var view = new Mock<IDatabaseStatisticsView>();
        viewFactory.Setup(f => f.Create<IDatabaseStatisticsView>()).Returns(view.Object);

        var presenter = new DatabaseStatisticsPresenter(service.Object, viewFactory.Object);

        presenter.ShowStatisticsDialog();

        Assert.Multiple(() =>
        {
            viewFactory.Verify(f => f.Create<IDatabaseStatisticsView>(), Times.Once);
            view.Verify(v => v.SetStatistics(stats), Times.Once);
            view.Verify(v => v.Show(), Times.Once);
        });
    }
}
