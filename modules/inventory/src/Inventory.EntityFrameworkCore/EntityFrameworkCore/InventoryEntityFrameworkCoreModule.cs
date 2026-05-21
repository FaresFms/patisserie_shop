using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Inventory.EntityFrameworkCore;

[DependsOn(
    typeof(InventoryDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class InventoryEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<InventoryDbContext>(options =>
        {
            options.AddDefaultRepositories<IInventoryDbContext>();
            
            /* Add custom repositories here. Example:
            * options.AddRepository<Question, EfCoreQuestionRepository>();
            */
        });
    }
}
