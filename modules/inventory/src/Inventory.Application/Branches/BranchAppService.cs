using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Inventory.Branches;

[Authorize(InventoryPermissions.Branches.Default)]
public class BranchAppService : InventoryAppService, IBranchAppService
{
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly BranchManager _branchManager;

    public BranchAppService(
        IRepository<AppBranch, Guid> branchRepository,
        BranchManager branchManager)
    {
        _branchRepository = branchRepository;
        _branchManager = branchManager;
    }

    public async Task<BranchDto> GetAsync(Guid id)
    {
        var branch = await _branchRepository.GetAsync(id);
        return ObjectMapper.Map<AppBranch, BranchDto>(branch);
    }

    public async Task<PagedResultDto<BranchDto>> GetListAsync(GetBranchesInput input)
    {
        var queryable = await BuildFilteredQueryAsync(input);

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? nameof(AppBranch.Name) : input.Sorting;
        var items = await AsyncExecuter.ToListAsync(
            queryable.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<BranchDto>(
            totalCount,
            items.Select(b => ObjectMapper.Map<AppBranch, BranchDto>(b)).ToList());
    }

    public async Task<List<BranchLookupDto>> GetLookupAsync()
    {
        var queryable = (await _branchRepository.GetQueryableAsync())
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name);

        var items = await AsyncExecuter.ToListAsync(queryable);
        return items.Select(b => ObjectMapper.Map<AppBranch, BranchLookupDto>(b)).ToList();
    }

    [Authorize(InventoryPermissions.Branches.Manage)]
    public async Task<BranchDto> CreateAsync(CreateBranchDto input)
    {
        var branch = await _branchManager.CreateAsync(
            input.Name,
            input.Address,
            input.Phone,
            input.Email,
            input.ManagerUserId,
            input.IsActive,
            input.BranchType);

        await _branchRepository.InsertAsync(branch, autoSave: true);
        return ObjectMapper.Map<AppBranch, BranchDto>(branch);
    }

    [Authorize(InventoryPermissions.Branches.Manage)]
    public async Task<BranchDto> UpdateAsync(Guid id, UpdateBranchDto input)
    {
        var branch = await _branchRepository.GetAsync(id);

        await _branchManager.ChangeNameAsync(branch, input.Name);
        branch.UpdateInfo(input.Address, input.Phone, input.Email, input.ManagerUserId, input.IsActive, input.BranchType);

        await _branchRepository.UpdateAsync(branch, autoSave: true);
        return ObjectMapper.Map<AppBranch, BranchDto>(branch);
    }

    [Authorize(InventoryPermissions.Branches.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _branchRepository.DeleteAsync(id);
    }

    private async Task<IQueryable<AppBranch>> BuildFilteredQueryAsync(GetBranchesInput input)
    {
        var queryable = await _branchRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLower();
            queryable = queryable.Where(b =>
                b.Name.ToLower().Contains(filter) ||
                (b.Address != null && b.Address.ToLower().Contains(filter)));
        }

        if (input.IsActive.HasValue)
        {
            queryable = queryable.Where(b => b.IsActive == input.IsActive.Value);
        }

        return queryable;
    }
}
