using Production.Localization;
using Volo.Abp.Application.Services;

namespace Production;

public abstract class ProductionAppService : ApplicationService
{
    protected ProductionAppService()
    {
        LocalizationResource = typeof(ProductionResource);
        ObjectMapperContext = typeof(ProductionApplicationModule);
    }
}
