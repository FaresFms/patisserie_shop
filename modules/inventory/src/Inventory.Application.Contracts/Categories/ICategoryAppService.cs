using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Inventory.Categories;

public interface ICategoryAppService : IApplicationService
{
    Task<CategoryDto> GetAsync(Guid id);

    Task<PagedResultDto<CategoryDto>> GetListAsync(GetCategoriesInput input);

    Task<CategoryDto> CreateAsync(CreateCategoryDto input);

    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryDto input);

    Task DeleteAsync(Guid id);
}
