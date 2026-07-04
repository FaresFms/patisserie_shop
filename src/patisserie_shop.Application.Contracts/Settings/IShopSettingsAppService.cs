using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Settings;

/// <summary>
/// Reads and writes the shop-wide settings (branding, receipt, operational
/// defaults). Reading is available to any authenticated user (the top bar and
/// cashier receipt need it); writing requires the shop-admin permission.
/// </summary>
public interface IShopSettingsAppService : IApplicationService
{
    Task<ShopSettingsDto> GetAsync();

    Task UpdateAsync(ShopSettingsDto input);
}
