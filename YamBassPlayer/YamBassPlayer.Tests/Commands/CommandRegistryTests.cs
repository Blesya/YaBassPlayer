namespace YamBassPlayer.Tests.Commands;

using Moq;
using YamBassPlayer.Commands;

[TestFixture]
public sealed class CommandRegistryTests
{
    private sealed class ArgsCapturingCommand : ICommand
    {
        public string Name => "test";
        public string Description => "тестовая команда";
        public IReadOnlyList<string> Aliases => ["t"];
        public string[]? LastArgs { get; private set; }

        public CommandResult Execute(string[] args)
        {
            LastArgs = args;
            return CommandResult.Ok("ок");
        }
    }

    private static ICommand CreateCommand(string name, params string[] aliases)
    {
        var mock = new Mock<ICommand>();
        mock.SetupGet(c => c.Name).Returns(name);
        mock.SetupGet(c => c.Description).Returns($"выполнено {name}");
        mock.SetupGet(c => c.Aliases).Returns(aliases);
        mock.Setup(c => c.Execute(It.IsAny<string[]>()))
            .Returns<string[]>(_ => CommandResult.Ok($"выполнено {name}"));
        return mock.Object;
    }

    // ──────────────── Разрешение по имени и алиасу ────────────────

    [Test]
    public void Execute_ResolvesByCanonicalName()
    {
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play", "p") });

        var result = registry.Execute("play");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.EqualTo("выполнено play"));
        });
    }

    [Test]
    public void Execute_ResolvesByAlias()
    {
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play", "p") });

        var result = registry.Execute("p");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.EqualTo("выполнено play"));
        });
    }

    [Test]
    public void Execute_IsCaseInsensitive()
    {
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play", "p") });

        Assert.That(registry.Execute("PLAY").Success, Is.True);
    }

    // ──────────────── Передача аргументов ────────────────

    [Test]
    public void Execute_PassesRemainingTokensAsArgs()
    {
        var cmd = new ArgsCapturingCommand();
        var registry = new CommandRegistry(new ICommand[] { cmd });

        registry.Execute("test   42  abc ");

        Assert.That(cmd.LastArgs, Is.EqualTo(new[] { "42", "abc" }));
    }

    // ──────────────── Ошибки ────────────────

    [Test]
    public void Execute_UnknownVerb_ReturnsError()
    {
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play", "p") });

        var result = registry.Execute("bogus");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Неизвестная команда"));
        });
    }

    [Test]
    public void Execute_EmptyInput_ReturnsError()
    {
        var registry = new CommandRegistry(Enumerable.Empty<ICommand>());

        var result = registry.Execute("   ");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Пустая команда"));
        });
    }

    [Test]
    public void Execute_SingleCommand_NoArguments_Works()
    {
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("pause") });

        Assert.That(registry.Execute("pause").Success, Is.True);
    }

    // ──────────────── help формируется из списка команд без цикла ────────────────

    [Test]
    public void Execute_Help_ReturnsCommandDescriptions()
    {
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play", "p"), CreateCommand("pause") });

        var result = registry.Execute("help");

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("выполнено play"));
            Assert.That(result.Message, Does.Contain("выполнено pause"));
        });
    }

    [Test]
    public void Execute_QuestionMarkAlias_ReturnsHelp()
    {
        var registry = new CommandRegistry(new ICommand[] { CreateCommand("play") });

        Assert.That(registry.Execute("?").Success, Is.True);
    }
}
