using Intelligence.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Intelligence;

public abstract class IntelligenceController : AbpControllerBase
{
    protected IntelligenceController()
    {
        LocalizationResource = typeof(IntelligenceResource);
    }
}
