using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Production.Entities;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Production.BranchRequests;

public class BranchProductionRequestManager : DomainService
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;

    public BranchProductionRequestManager(
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository)
    {
        _branchRepository = branchRepository;
        _productRepository = productRepository;
    }

    public async Task<AppBranchProductionRequest> CreateAsync(
        Guid branchId,
        DateTime neededByDate,
        string priority,
        Guid? requestedByUserId,
        string? notes)
    {
        await EnsureSalesBranchAsync(branchId);

        return new AppBranchProductionRequest(
            GuidGenerator.Create(),
            CreateRequestNumber(),
            branchId,
            neededByDate,
            priority,
            requestedByUserId,
            notes);
    }

    public async Task EnsureSalesBranchAsync(Guid branchId)
    {
        var branch = await _branchRepository.GetAsync(branchId);
        if (branch.BranchType != BranchTypes.SalesBranch)
        {
            throw new BusinessException(ProductionErrorCodes.BranchMustBeSalesBranch)
                .WithData("BranchId", branchId)
                .WithData("BranchType", branch.BranchType);
        }
    }

    public async Task EnsureBranchAccessAsync(Guid branchId, Guid? userId, bool bypass)
    {
        if (bypass)
        {
            return;
        }

        if (userId == null ||
            !await _branchRepository.AnyAsync(b => b.Id == branchId && b.ManagerUserId == userId))
        {
            throw new BusinessException(ProductionErrorCodes.BranchRequestAccessDenied)
                .WithData("BranchId", branchId);
        }
    }

    public async Task<List<AppBranch>> GetRequestableBranchesAsync(Guid? userId, bool bypass)
    {
        List<AppBranch> branches;
        if (bypass)
        {
            branches = await _branchRepository.GetListAsync(
                b => b.IsActive && b.BranchType == BranchTypes.SalesBranch);
        }
        else if (userId.HasValue)
        {
            branches = await _branchRepository.GetListAsync(
                b => b.IsActive &&
                     b.BranchType == BranchTypes.SalesBranch &&
                     b.ManagerUserId == userId.Value);
        }
        else
        {
            branches = [];
        }

        branches.Sort((left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
        return branches;
    }

    public async Task EnsureRequestProductsAreProducibleAsync(IEnumerable<Guid> productIds)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var products = await _productRepository.GetListAsync(p => ids.Contains(p.Id));
        var byId = products.ToDictionary(p => p.Id);

        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var product))
            {
                await _productRepository.GetAsync(id);
                continue;
            }

            if (!product.IsActive || product.ProductType != ProductTypes.FinishedGood || !product.IsProducible)
            {
                throw new BusinessException(ProductionErrorCodes.RequestedProductNotProducible)
                    .WithData("ProductId", id)
                    .WithData("ProductType", product.ProductType);
            }
        }
    }

    private static string CreateRequestNumber() => $"REQ-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";
}
