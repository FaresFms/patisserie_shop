using Intelligence.Localization;
using Volo.Abp.Application.Services;

namespace Intelligence;

public abstract class IntelligenceAppService : ApplicationService
{
    protected IntelligenceAppService()
    {
        LocalizationResource = typeof(IntelligenceResource);
        ObjectMapperContext = typeof(IntelligenceApplicationModule);
    }
}
