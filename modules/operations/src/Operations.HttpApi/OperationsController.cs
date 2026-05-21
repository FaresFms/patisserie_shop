using Operations.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Operations;

public abstract class OperationsController : AbpControllerBase
{
    protected OperationsController()
    {
        LocalizationResource = typeof(OperationsResource);
    }
}
