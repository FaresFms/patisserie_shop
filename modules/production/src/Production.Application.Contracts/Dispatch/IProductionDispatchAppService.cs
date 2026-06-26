using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Production.Dispatch;

public interface IProductionDispatchAppService : IApplicationService
{
    Task<ProductionDispatchResultDto> CreateTransferAsync(CreateProductionDispatchTransferDto input);
}
