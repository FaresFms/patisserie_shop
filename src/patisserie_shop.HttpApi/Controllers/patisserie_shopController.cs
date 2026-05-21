using patisserie_shop.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace patisserie_shop.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class patisserie_shopController : AbpControllerBase
{
    protected patisserie_shopController()
    {
        LocalizationResource = typeof(patisserie_shopResource);
    }
}
