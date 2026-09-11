using Autofac;

namespace YamBassPlayer.Views.Impl;

/// <summary>
/// Реализация <see cref="IViewFactory"/> поверх Autofac: резолвит вьюхи из контейнера.
/// </summary>
public sealed class AutofacViewFactory(IComponentContext context) : IViewFactory
{
    public TView Create<TView>() where TView : class => context.Resolve<TView>();
}
