using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Intelligence.Velocity;

public interface IProductVelocityAppService : IApplicationService
{
    Task<PagedResultDto<ProductVelocityDto>> GetListAsync(GetProductVelocitiesInput input);
}
