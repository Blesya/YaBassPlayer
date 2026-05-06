using Autofac;
using Autofac.Core;

namespace YamBassPlayer;

public static class AutofacExtensions
{
    // Для обычных типов: builder.RegisterSingleton<IInterface, Implementation>();
    public static void RegisterSingleton<TInterface, TImplementation>(this ContainerBuilder builder)
        where TImplementation : TInterface
    {
        builder.RegisterType<TImplementation>().As<TInterface>().SingleInstance();
    }

    // Для типов, которые регистрируются как Self: builder.RegisterSingletonSelf<Implementation>();
    public static void RegisterSingletonSelf<TImplementation>(this ContainerBuilder builder)
        where TImplementation : class
    {
        builder.RegisterType<TImplementation>().AsSelf().SingleInstance();
    }

    // Для фабричных методов (лямбд): builder.RegisterSingleton<IInterface>(c => new Implementation(c.Resolve...));
    public static void RegisterSingleton<TInterface>(this ContainerBuilder builder, Func<IComponentContext, TInterface> registration)
    {
        builder.Register(registration).As<TInterface>().SingleInstance();
    }

    // Для регистраций с именами (Named): builder.RegisterNamedSingleton<TInterface>("name", c => ...);
    public static void RegisterNamedSingleton<TInterface>(this ContainerBuilder builder, string name, Func<IComponentContext, TInterface> registration)
    {
        builder.Register(registration).Named<TInterface>(name).SingleInstance();
    }
}