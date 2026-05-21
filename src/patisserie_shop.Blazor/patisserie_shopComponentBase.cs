using patisserie_shop.Localization;
using Volo.Abp.AspNetCore.Components;

namespace patisserie_shop.Blazor;

public abstract class patisserie_shopComponentBase : AbpComponentBase
{
    protected patisserie_shopComponentBase()
    {
        LocalizationResource = typeof(patisserie_shopResource);
    }
}
