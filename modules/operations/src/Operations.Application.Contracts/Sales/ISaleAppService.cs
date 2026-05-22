using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Operations.Sales;

public interface ISaleAppService : IApplicationService
{
    Task<SaleDto> GetAsync(Guid id);
    Task<PagedResultDto<SaleDto>> GetListAsync(GetSalesInput input);

    /// <summary>Atomic create — sale + items + stock adjustments + movement rows, in one UoW.</summary>
    Task<SaleDto> CreateAsync(CreateSaleDto input);

    Task DeleteAsync(Guid id);

    /// <summary>Active products at the given branch with QuantityOnHand &gt; 0, for the create modal picker.</summary>
    Task<List<SaleProductLookupDto>> GetAvailableProductsAsync(Guid branchId);

    /// <summary>Branches the current user can record sales for (all if ManageAll, otherwise own).</summary>
    Task<List<Guid>> GetAccessibleBranchIdsAsync();
}
