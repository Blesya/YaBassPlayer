namespace YamBassPlayer.Tests.Commands;

using Moq;
using YamBassPlayer.Commands;
using YamBassPlayer.Presenters.Impl;
using YamBassPlayer.Views;

[TestFixture]
public sealed class CommandInputPresenterTests
{
    private static ICommand CreateCommand(string name, string message)
    {
        var mock = new Mock<ICommand>();
        mock.SetupGet(c => c.Name).Returns(name);
        mock.SetupGet(c => c.Aliases).Returns([]);
        mock.Setup(c => c.Execute(It.IsAny<string[]>()))
            .Returns(CommandResult.Ok(message));
        return mock.Object;
    }

    // ──────────────── Результат показывается в виде ────────────────

    [Test]
    public void HandleSubmitted_ShowsResultMessageInView()
    {
        var view = new Mock<ICommandInputView>();
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play", "выполнено") });
        var presenter = new CommandInputPresenter(view.Object, registry);

        presenter.HandleSubmitted("play");

        view.Verify(v => v.SetResult("выполнено"), Times.Once);
    }

    // ──────────────── Ошибка показывается в виде ────────────────

    [Test]
    public void HandleSubmitted_ShowsErrorForUnknownCommand()
    {
        var view = new Mock<ICommandInputView>();
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play", "выполнено") });
        var presenter = new CommandInputPresenter(view.Object, registry);

        presenter.HandleSubmitted("nope");

        view.Verify(v => v.SetResult(It.Is<string>(s => s.Contains("Неизвестная команда"))), Times.Once);
    }

    // ──────────────── Событие view соединено с presenter ────────────────

    [Test]
    public void Constructor_SubscribesToViewSubmitEvent()
    {
        var view = new Mock<ICommandInputView>();
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play", "выполнено") });
        _ = new CommandInputPresenter(view.Object, registry);

        view.Raise(v => v.OnCommandSubmitted += null, "play");

        view.Verify(v => v.SetResult("выполнено"), Times.Once);
    }
}
