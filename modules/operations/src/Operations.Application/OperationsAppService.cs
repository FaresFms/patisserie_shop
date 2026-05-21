using Operations.Localization;
using Volo.Abp.Application.Services;

namespace Operations;

public abstract class OperationsAppService : ApplicationService
{
    protected OperationsAppService()
    {
        LocalizationResource = typeof(OperationsResource);
        ObjectMapperContext = typeof(OperationsApplicationModule);
    }
}
