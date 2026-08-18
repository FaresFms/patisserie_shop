using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.Entities;
using Inventory.Localization;
using Inventory.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Inventory.Branches;

public class BranchAppService : InventoryAppService, IBranchAppService
{
    private const int LookupMaxResults = 200;

    private readonly IBranchRepository _branchRepository;
    private readonly BranchManager _branchManager;

    public BranchAppService(
        IBranchRepository branchRepository,
        BranchManager branchManager)
    {
        _branchRepository = branchRepository;
        _branchManager = branchManager;
    }

    [Authorize(InventoryPermissions.Branches.Default)]
    public async Task<BranchDto> GetAsync(Guid id)
    {
        var branch = await _branchRepository.GetAsync(id);
        return MapToDto(branch);
    }

    [Authorize(InventoryPermissions.Branches.Default)]
    public async Task<PagedResultDto<BranchDto>> GetListAsync(GetBranchesInput input)
    {
        var totalCount = await _branchRepository.CountFilteredAsync(input.Filter, input.IsActive);
        var items = await _branchRepository.GetFilteredListAsync(
            input.Filter,
            input.IsActive,
            input.Sorting ?? string.Empty,
            input.SkipCount,
            input.MaxResultCount);

        return new PagedResultDto<BranchDto>(
            totalCount,
            items.ConvertAll(MapToDto));
    }

    // Branch names are shared lookup data used by permission-scoped screens in
    // several modules. The consuming screen/service still enforces its feature
    // permission and branch scope; this endpoint only requires authentication.
    [Authorize]
    public async Task<List<BranchLookupDto>> GetLookupAsync()
    {
        var items = await _branchRepository.GetActiveLookupAsync(LookupMaxResults);
        return items.ConvertAll(MapToLookupDto);
    }

    [Authorize(InventoryPermissions.Branches.Manage)]
    public async Task<BranchDto> CreateAsync(CreateBranchDto input)
    {
        var branch = await _branchManager.CreateAsync(
            input.NameAr,
            input.NameEn,
            input.AddressAr,
            input.AddressEn,
            input.Phone,
            input.Email,
            input.ManagerUserId,
            input.IsActive,
            input.BranchType);

        await _branchRepository.InsertAsync(branch, autoSave: true);
        return MapToDto(branch);
    }

    [Authorize(InventoryPermissions.Branches.Manage)]
    public async Task<BranchDto> UpdateAsync(Guid id, UpdateBranchDto input)
    {
        var branch = await _branchRepository.GetAsync(id);

        await _branchManager.ChangeNamesAsync(branch, input.NameAr, input.NameEn);
        branch.UpdateInfo(
            input.AddressAr,
            input.AddressEn,
            input.Phone,
            input.Email,
            input.ManagerUserId,
            input.IsActive,
            input.BranchType);

        await _branchRepository.UpdateAsync(branch, autoSave: true);
        return MapToDto(branch);
    }

    [Authorize(InventoryPermissions.Branches.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        await _branchRepository.DeleteAsync(id);
    }

    private BranchDto MapToDto(AppBranch branch)
    {
        var dto = ObjectMapper.Map<AppBranch, BranchDto>(branch);
        dto.Name = LocalizedBusinessText.Select(branch.NameAr, branch.NameEn);
        dto.Address = LocalizedBusinessText.Select(branch.AddressAr, branch.AddressEn);
        return dto;
    }

    private BranchLookupDto MapToLookupDto(AppBranch branch)
    {
        var dto = ObjectMapper.Map<AppBranch, BranchLookupDto>(branch);
        dto.Name = LocalizedBusinessText.Select(branch.NameAr, branch.NameEn);
        return dto;
    }
}
