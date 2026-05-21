using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Inventory.Samples;

public interface ISampleAppService : IApplicationService
{
    Task<SampleDto> GetAsync();

    Task<SampleDto> GetAuthorizedAsync();
}
