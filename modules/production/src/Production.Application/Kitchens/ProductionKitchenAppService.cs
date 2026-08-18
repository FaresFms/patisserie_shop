using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Production.Permissions;

namespace Production.Kitchens;

[Authorize]
public class ProductionKitchenAppService : ProductionAppService, IProductionKitchenAppService
{
    private readonly KitchenAccessChecker _kitchenAccessChecker;

    public ProductionKitchenAppService(KitchenAccessChecker kitchenAccessChecker)
    {
        _kitchenAccessChecker = kitchenAccessChecker;
    }

    public async Task<List<KitchenBranchLookupDto>> GetAccessibleLookupAsync()
    {
        var kitchens = await _kitchenAccessChecker.GetAccessibleKitchensAsync();
        var result = new List<KitchenBranchLookupDto>(kitchens.Count);
        foreach (var kitchen in kitchens)
        {
            result.Add(new KitchenBranchLookupDto
            {
                Id = kitchen.Id,
                Name = kitchen.DisplayName
            });
        }

        return result;
    }
}
