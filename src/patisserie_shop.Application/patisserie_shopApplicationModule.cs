using Inventory;
using Operations;
using Intelligence;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Account;
using Volo.Abp.Identity;
using Volo.Abp.Mapperly;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace patisserie_shop;

[DependsOn(
    typeof(InventoryApplicationModule),
    typeof(OperationsApplicationModule),
    typeof(IntelligenceApplicationModule),
    typeof(patisserie_shopDomainModule),
    typeof(patisserie_shopApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpAccountApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class patisserie_shopApplicationModule : AbpModule
{

}
