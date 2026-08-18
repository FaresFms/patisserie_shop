using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Production;
using Production.Control;
using Production.Permissions;
using Volo.Abp;
using Volo.Abp.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace patisserie_shop;

[Authorize(ProductionPermissions.Control.Default)]
public class ProductionControlAppService : patisserie_shopAppService, IProductionControlAppService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly IIdentityUserRepository _userRepository;

    public ProductionControlAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        IIdentityUserRepository userRepository)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _userRepository = userRepository;
    }

    public async Task<ProductionControlProfileDto> GetAsync()
    {
        var json = await _settingProvider.GetOrNullAsync(ProductionControlSettings.Profile);
        return Deserialize(json);
    }

    [Authorize(ProductionPermissions.Control.Manage)]
    public async Task UpdateAsync(ProductionControlProfileDto input)
    {
        Validate(input);
        var json = JsonSerializer.Serialize(input, JsonOptions);
        await _settingManager.SetGlobalAsync(ProductionControlSettings.Profile, json);
    }

    public async Task<List<ProductionOperatorLookupDto>> GetOperatorsAsync(string? filter = null)
    {
        var users = await _userRepository.GetListAsync(
            sorting: nameof(IdentityUser.Name),
            maxResultCount: 200,
            skipCount: 0,
            filter: filter);

        return users
            .Where(user => user.IsActive)
            .Select(user => new ProductionOperatorLookupDto
            {
                Id = user.Id,
                Name = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name,
                UserName = user.UserName
            })
            .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProductionControlProfileDto Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ProductionControlProfileDto();
        }

        try
        {
            return JsonSerializer.Deserialize<ProductionControlProfileDto>(json, JsonOptions)
                ?? new ProductionControlProfileDto();
        }
        catch (JsonException)
        {
            return new ProductionControlProfileDto();
        }
    }

    private static void Validate(ProductionControlProfileDto input)
    {
        var workCenterCodes = input.WorkCenters
            .Select(item => item.Code?.Trim())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToList();
        if (workCenterCodes.Count != workCenterCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new BusinessException(ProductionErrorCodes.InvalidProductionSchedule)
                .WithData("Reason", "DuplicateWorkCenterCode");
        }

        var shiftCodes = input.Shifts
            .Select(item => item.Code?.Trim())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToList();
        if (shiftCodes.Count != shiftCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            || input.Shifts.Any(shift => shift.StartTime == shift.EndTime))
        {
            throw new BusinessException(ProductionErrorCodes.InvalidProductionSchedule)
                .WithData("Reason", "InvalidShift");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
