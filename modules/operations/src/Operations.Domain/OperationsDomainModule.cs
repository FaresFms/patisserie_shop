using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Operations;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(OperationsDomainSharedModule)
)]
public class OperationsDomainModule : AbpModule
{

}
