using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Story;

/// <summary>
/// Host-level read-model that merges the immutable ledgers of one product at one
/// branch — stock movements (Inventory), decision logs (Intelligence) and stock
/// batches (Inventory) — into a single newest-first narrative timeline. Same
/// cross-module, in-memory composition pattern as
/// <see cref="patisserie_shop.Analytics.ISalesAnalyticsAppService"/>; branch
/// access follows the BranchInventory precedent (inaccessible branch → error).
/// </summary>
public interface IProductStoryAppService : IApplicationService
{
    Task<ProductStoryDto> GetAsync(GetProductStoryInput input);
}
