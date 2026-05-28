using System;
using System.Threading.Tasks;
using Inventory.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Inventory.Suppliers;

public class SupplierManager : DomainService
{
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;

    public SupplierManager(IRepository<AppSupplier, Guid> supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<AppSupplier> CreateAsync(
        string name,
        string? contactPerson = null,
        string? phone = null,
        string? email = null,
        string? address = null,
        bool isActive = true)
    {
        await EnsureNameIsUniqueAsync(name);

        return new AppSupplier(
            GuidGenerator.Create(),
            name,
            contactPerson,
            phone,
            email,
            address,
            isActive);
    }

    public async Task ChangeNameAsync(AppSupplier supplier, string newName)
    {
        Check.NotNull(supplier, nameof(supplier));
        Check.NotNullOrWhiteSpace(newName, nameof(newName));

        var normalized = newName.Trim();
        if (string.Equals(supplier.Name, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await EnsureNameIsUniqueAsync(normalized, supplier.Id);
        supplier.SetName(normalized);
    }

    private async Task EnsureNameIsUniqueAsync(string name, Guid? ignoreId = null)
    {
        var normalized = name.Trim();
        var exists = await _supplierRepository.AnyAsync(s =>
            s.Name == normalized && (ignoreId == null || s.Id != ignoreId.Value));

        if (exists)
        {
            throw new BusinessException(InventoryErrorCodes.DuplicateSupplierName)
                .WithData("Name", normalized);
        }
    }
}
