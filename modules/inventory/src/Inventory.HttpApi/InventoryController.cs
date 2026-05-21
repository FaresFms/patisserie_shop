using Inventory.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Inventory;

public abstract class InventoryController : AbpControllerBase
{
    protected InventoryController()
    {
        LocalizationResource = typeof(InventoryResource);
    }
}
