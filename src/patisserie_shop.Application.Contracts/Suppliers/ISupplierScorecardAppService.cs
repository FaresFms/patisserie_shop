using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace patisserie_shop.Suppliers;

/// <summary>
/// Host-level read-model that composes Operations purchase-order delivery history
/// with the Inventory supplier records into per-supplier scorecards. Supplier-global
/// (not branch-scoped); gated on the Inventory Suppliers.Default permission.
/// </summary>
public interface ISupplierScorecardAppService : IApplicationService
{
    Task<List<SupplierScorecardDto>> GetListAsync(GetSupplierScorecardsInput input);
}
