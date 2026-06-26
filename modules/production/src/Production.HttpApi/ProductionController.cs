using Production.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Production;

public abstract class ProductionController : AbpControllerBase
{
    protected ProductionController()
    {
        LocalizationResource = typeof(ProductionResource);
    }
}
