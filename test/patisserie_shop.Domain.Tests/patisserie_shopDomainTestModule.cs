using Volo.Abp.Modularity;

namespace patisserie_shop;

[DependsOn(
    typeof(patisserie_shopDomainModule),
    typeof(patisserie_shopTestBaseModule)
)]
public class patisserie_shopDomainTestModule : AbpModule
{

}
