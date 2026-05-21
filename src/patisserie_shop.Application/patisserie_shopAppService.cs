using patisserie_shop.Localization;
using Volo.Abp.Application.Services;

namespace patisserie_shop;

/* Inherit your application services from this class.
 */
public abstract class patisserie_shopAppService : ApplicationService
{
    protected patisserie_shopAppService()
    {
        LocalizationResource = typeof(patisserie_shopResource);
    }
}
