using Volo.Abp.Modularity;

namespace patisserie_shop;

[DependsOn(
    typeof(patisserie_shopApplicationModule),
    typeof(patisserie_shopDomainTestModule)
)]
public class patisserie_shopApplicationTestModule : AbpModule
{

}
