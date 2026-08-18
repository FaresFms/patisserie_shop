using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Production.Control;

public interface IProductionControlAppService : IApplicationService
{
    Task<ProductionControlProfileDto> GetAsync();
    Task UpdateAsync(ProductionControlProfileDto input);
    Task<List<ProductionOperatorLookupDto>> GetOperatorsAsync(string? filter = null);
}
