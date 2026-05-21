using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Intelligence.EntityFrameworkCore;

[DependsOn(
    typeof(IntelligenceDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class IntelligenceEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<IntelligenceDbContext>(options =>
        {
            options.AddDefaultRepositories<IIntelligenceDbContext>();
            
            /* Add custom repositories here. Example:
            * options.AddRepository<Question, EfCoreQuestionRepository>();
            */
        });
    }
}
