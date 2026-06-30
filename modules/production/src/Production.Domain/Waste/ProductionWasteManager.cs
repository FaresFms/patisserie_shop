using System;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Production.Entities;

namespace Production.Waste;

public class ProductionWasteManager : DomainService
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public ProductionWasteManager(
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository)
    {
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<AppProductionWaste> CreateAsync(
        Guid? productionOrderId,
        Guid kitchenBranchId,
        Guid productId,
        string wasteType,
        int quantity,
        decimal unitCost,
        string reason,
        Guid? recordedByUserId,
        DateTime recordedAt,
        string? notes = null)
    {
        var branch = await _branchRepository.GetAsync(kitchenBranchId);
        if (branch.BranchType != BranchTypes.MainKitchen)
        {
            throw new Volo.Abp.BusinessException(ProductionErrorCodes.KitchenBranchMustBeMainKitchen)
                .WithData("BranchId", kitchenBranchId)
                .WithData("BranchType", branch.BranchType);
        }

        await _productRepository.GetAsync(productId);

        return new AppProductionWaste(
            GuidGenerator.Create(),
            productionOrderId,
            kitchenBranchId,
            productId,
            wasteType,
            quantity,
            unitCost,
            reason,
            recordedByUserId,
            recordedAt,
            notes);
    }
}
