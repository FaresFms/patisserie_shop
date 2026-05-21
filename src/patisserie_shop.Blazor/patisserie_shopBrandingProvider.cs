using Microsoft.Extensions.Localization;
using patisserie_shop.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace patisserie_shop.Blazor;

[Dependency(ReplaceServices = true)]
public class patisserie_shopBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<patisserie_shopResource> _localizer;

    public patisserie_shopBrandingProvider(IStringLocalizer<patisserie_shopResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
