using Volo.Abp.Modularity;

namespace patisserie_shop;

/* Inherit from this class for your domain layer tests. */
public abstract class patisserie_shopDomainTestBase<TStartupModule> : patisserie_shopTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
