namespace YamBassPlayer.Views;

/// <summary>
/// Создаёт вьюхи по требованию, убирая из презентеров прямые <c>new</c> и
/// <c>ServicesProvider.Ioc.Resolve</c>. В тестах подменяется фабрикой, отдающей mock-вьюхи.
/// </summary>
public interface IViewFactory
{
    TView Create<TView>() where TView : class;
}
