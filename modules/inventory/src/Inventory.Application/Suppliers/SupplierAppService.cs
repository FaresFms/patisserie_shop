using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Inventory.Suppliers;

[Authorize(InventoryPermissions.Suppliers.Default)]
public class SupplierAppService : InventoryAppService, ISupplierAppService
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly SupplierManager _supplierManager;

    public SupplierAppService(
        ISupplierRepository supplierRepository,
        SupplierManager supplierManager)
    {
        _supplierRepository = supplierRepository;
        _supplierManager = supplierManager;
    }

    public async Task<SupplierDto> GetAsync(Guid id)
    {
        var supplier = await _supplierRepository.GetAsync(id);
        return ObjectMapper.Map<AppSupplier, SupplierDto>(supplier);
    }

    public async Task<PagedResultDto<SupplierDto>> GetListAsync(GetSuppliersInput input)
    {
        var totalCount = await _supplierRepository.CountFilteredAsync(input.Filter, input.IsActive);

        var items = await _supplierRepository.GetFilteredListAsync(
            input.Filter,
            input.IsActive,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<SupplierDto>(
            totalCount,
            [.. items.ConvertAll(s => ObjectMapper.Map<AppSupplier, SupplierDto>(s))]);
    }

    [Authorize(InventoryPermissions.Suppliers.Manage)]
    public async Task<SupplierDto> CreateAsync(CreateSupplierDto input)
    {
        var supplier = await _supplierManager.CreateAsync(
            input.Name,
            input.ContactPerson,
            input.Phone,
            input.Email,
            input.Address,
            input.IsActive,
            input.LeadTimeDays);

        await _supplierRepository.InsertAsync(supplier, autoSave: true);
        return ObjectMapper.Map<AppSupplier, SupplierDto>(supplier);
    }

    [Authorize(InventoryPermissions.Suppliers.Manage)]
    public async Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierDto input)
    {
        var supplier = await _supplierRepository.GetAsync(id);

        await _supplierManager.ChangeNameAsync(supplier, input.Name);
        supplier.UpdateInfo(input.ContactPerson, input.Phone, input.Email, input.Address, input.IsActive, input.LeadTimeDays);

        await _supplierRepository.UpdateAsync(supplier, autoSave: true);
        return ObjectMapper.Map<AppSupplier, SupplierDto>(supplier);
    }

    [Authorize(InventoryPermissions.Suppliers.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _supplierRepository.DeleteAsync(id);
    }
}
