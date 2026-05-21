using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Inventory.Products;

public interface IProductAppService : IApplicationService
{
    Task<ProductDto> GetAsync(Guid id);

    Task<PagedResultDto<ProductDto>> GetListAsync(GetProductsInput input);

    Task<List<ProductLookupDto>> GetLookupAsync(string? filter = null);

    Task<ProductDto> CreateAsync(CreateProductDto input);

    Task<ProductDto> UpdateAsync(Guid id, UpdateProductDto input);

    Task DeleteAsync(Guid id);
}
