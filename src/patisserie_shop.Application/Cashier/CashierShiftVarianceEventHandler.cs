using System;
using System.Globalization;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Localization;
using Inventory.Entities;
using Microsoft.Extensions.Localization;
using Operations.Events;
using patisserie_shop.Localization;
using patisserie_shop.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Settings;

namespace patisserie_shop.Cashier;

public class CashierShiftVarianceEventHandler
    : IDistributedEventHandler<CashierShiftVarianceEto>, ITransientDependency
{
    private readonly IRepository<AppDecisionLog, Guid> _decisionLogRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IStringLocalizer<patisserie_shopResource> _localizer;
    private readonly ISettingProvider _settingProvider;

    public CashierShiftVarianceEventHandler(
        IRepository<AppDecisionLog, Guid> decisionLogRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IGuidGenerator guidGenerator,
        IStringLocalizer<patisserie_shopResource> localizer,
        ISettingProvider settingProvider)
    {
        _decisionLogRepository = decisionLogRepository;
        _branchRepository = branchRepository;
        _userRepository = userRepository;
        _guidGenerator = guidGenerator;
        _localizer = localizer;
        _settingProvider = settingProvider;
    }

    public async Task HandleEventAsync(CashierShiftVarianceEto eventData)
    {
        using var contentCulture = PersistedContentCulture.UseArabic();

        if (eventData.Variance == 0)
        {
            return;
        }

        var shiftToken = eventData.ShiftId.ToString("N");
        var alreadyLogged = await _decisionLogRepository.AnyAsync(l =>
            l.DecisionType == DecisionTypes.CashierVariance
            && l.Reasoning.Contains(shiftToken));
        if (alreadyLogged)
        {
            return;
        }

        var branch = await _branchRepository.FindAsync(eventData.BranchId);
        var cashier = await _userRepository.FindAsync(eventData.CashierUserId);
        var branchName = branch?.Name ?? eventData.BranchId.ToString();
        var cashierName = cashier?.UserName ?? eventData.CashierUserId.ToString();
        var varianceKind = eventData.Variance > 0
            ? _localizer["CashierVariance:Over"]
            : _localizer["CashierVariance:Short"];
        var currency = await GetDefaultCurrencyAsync();

        var reasoning = _localizer[
            "CashierVariance:Reasoning",
            cashierName,
            shiftToken,
            branchName,
            FormatMoney(eventData.ExpectedCash, currency),
            FormatMoney(eventData.CountedCash, currency),
            FormatMoney(eventData.Variance, currency),
            varianceKind];

        var log = new AppDecisionLog(
            id: _guidGenerator.Create(),
            ruleId: IntelligenceConstants.CashierVarianceRuleId,
            productId: Guid.Empty,
            branchId: eventData.BranchId,
            decisionType: DecisionTypes.CashierVariance,
            reasoning: reasoning,
            suggestedAction: _localizer["CashierVariance:SuggestedAction"]);

        await _decisionLogRepository.InsertAsync(log, autoSave: true);
    }

    private async Task<string> GetDefaultCurrencyAsync()
    {
        var currency = (await _settingProvider.GetOrNullAsync(patisserie_shopSettings.DefaultCurrency))
            ?.Trim()
            .ToUpperInvariant();

        return currency?.Length == 3 ? currency : "USD";
    }

    private static string FormatMoney(decimal value, string currency)
        => $"{value.ToString("N2", CultureInfo.CurrentCulture)} {currency}";
}
