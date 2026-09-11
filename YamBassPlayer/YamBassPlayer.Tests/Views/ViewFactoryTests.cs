using Autofac;
using YamBassPlayer.Views;
using YamBassPlayer.Views.Impl;

namespace YamBassPlayer.Tests.Views;

/// <summary>
/// Проверяет, что DI-фабрика вьюх действительно резолвит зарегистрированные вьюхи.
/// Наследует headless-харнесс, т.к. конструкторы Terminal.Gui-вьюх требуют
/// инициализированного <c>Application</c>.
/// </summary>
[TestFixture]
[NonParallelizable]
public sealed class ViewFactoryTests : ViewTestBase
{
    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterType<AutofacViewFactory>().As<IViewFactory>().SingleInstance();
        builder.RegisterType<EqualizerView>().As<IEqualizerView>();
        builder.RegisterType<DatabaseStatisticsView>().As<IDatabaseStatisticsView>();
        return builder.Build();
    }

    [Test]
    public void AutofacViewFactory_ResolvesRegisteredViews()
    {
        using IContainer container = BuildContainer();
        var factory = container.Resolve<IViewFactory>();

        Assert.Multiple(() =>
        {
            Assert.That(factory.Create<IEqualizerView>(), Is.Not.Null);
            Assert.That(factory.Create<IDatabaseStatisticsView>(), Is.Not.Null);
        });
    }

    [Test]
    public void AutofacViewFactory_CreatesNewTransientViewEachTime()
    {
        using IContainer container = BuildContainer();
        var factory = container.Resolve<IViewFactory>();

        Assert.That(factory.Create<IEqualizerView>(), Is.Not.SameAs(factory.Create<IEqualizerView>()));
    }
}
