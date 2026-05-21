using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace Intelligence;

[DependsOn(
    typeof(IntelligenceDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class IntelligenceApplicationContractsModule : AbpModule
{

}
