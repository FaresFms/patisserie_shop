using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Operations.EntityFrameworkCore;

[DependsOn(
    typeof(OperationsDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class OperationsEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<OperationsDbContext>(options =>
        {
            options.AddDefaultRepositories<IOperationsDbContext>();
            
            /* Add custom repositories here. Example:
            * options.AddRepository<Question, EfCoreQuestionRepository>();
            */
        });
    }
}
