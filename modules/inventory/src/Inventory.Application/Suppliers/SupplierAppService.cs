using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Suppliers;

[Authorize(InventoryPermissions.Suppliers.Default)]
public class SupplierAppService : InventoryAppService, ISupplierAppService
{
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;

    public SupplierAppService(IRepository<AppSupplier, Guid> supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<SupplierDto> GetAsync(Guid id)
    {
        var supplier = await _supplierRepository.GetAsync(id);
        return ObjectMapper.Map<AppSupplier, SupplierDto>(supplier);
    }

    public async Task<PagedResultDto<SupplierDto>> GetListAsync(GetSuppliersInput input)
    {
        var queryable = await _supplierRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            queryable = queryable.Where(s => s.Name.ToLower().Contains(filter));
        }

        if (input.IsActive.HasValue)
        {
            queryable = queryable.Where(s => s.IsActive == input.IsActive.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? nameof(AppSupplier.Name) : input.Sorting;
        queryable = queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount);

        var items = await AsyncExecuter.ToListAsync(queryable);

        return new PagedResultDto<SupplierDto>(
            totalCount,
            items.Select(s => ObjectMapper.Map<AppSupplier, SupplierDto>(s)).ToList()
        );
    }

    [Authorize(InventoryPermissions.Suppliers.Manage)]
    public async Task<SupplierDto> CreateAsync(CreateSupplierDto input)
    {
        var supplier = new AppSupplier(
            GuidGenerator.Create(),
            input.Name,
            input.ContactPerson,
            input.Phone,
            input.Email,
            input.Address,
            input.IsActive
        );

        await _supplierRepository.InsertAsync(supplier, autoSave: true);
        return ObjectMapper.Map<AppSupplier, SupplierDto>(supplier);
    }

    [Authorize(InventoryPermissions.Suppliers.Manage)]
    public async Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierDto input)
    {
        var supplier = await _supplierRepository.GetAsync(id);
        supplier.Name = input.Name;
        supplier.ContactPerson = input.ContactPerson;
        supplier.Phone = input.Phone;
        supplier.Email = input.Email;
        supplier.Address = input.Address;
        supplier.IsActive = input.IsActive;
        await _supplierRepository.UpdateAsync(supplier, autoSave: true);
        return ObjectMapper.Map<AppSupplier, SupplierDto>(supplier);
    }

    [Authorize(InventoryPermissions.Suppliers.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _supplierRepository.DeleteAsync(id);
    }
}
