using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Production.Kitchens;

public interface IProductionKitchenAppService : IApplicationService
{
    Task<List<KitchenBranchLookupDto>> GetAccessibleLookupAsync();
}
