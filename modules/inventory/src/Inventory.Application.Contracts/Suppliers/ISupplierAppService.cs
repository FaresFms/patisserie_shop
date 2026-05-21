using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Inventory.Suppliers;

public interface ISupplierAppService : IApplicationService
{
    Task<SupplierDto> GetAsync(Guid id);

    Task<PagedResultDto<SupplierDto>> GetListAsync(GetSuppliersInput input);

    Task<SupplierDto> CreateAsync(CreateSupplierDto input);

    Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierDto input);

    Task DeleteAsync(Guid id);
}
