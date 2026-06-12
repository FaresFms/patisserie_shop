using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Intelligence.Decisions;
using Intelligence.Events;
using Intelligence.Rules;
using Microsoft.Extensions.Logging;
using Operations.PurchaseOrders;
using Operations.StockTransfers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace patisserie_shop.Decisions;

/// <summary>
/// Rule "autopilot": reacts to every <see cref="DecisionMadeEto"/> and, when the
/// originating rule's ActionMode asks for it, immediately executes the decision
/// through <see cref="IDecisionActionAppService"/> (creating the corrective draft
/// document and flipping the decision to Executed). AutoSubmit additionally
/// submits the created PO / transfer for approval — a human still approves the
/// document, so this is configurable automation with a human-in-the-loop.
///
/// Authorization: app services carry [Authorize] but event handlers run with no
/// current user, so the whole body runs inside an
/// <see cref="ICurrentPrincipalAccessor.Change"/> scope impersonating the seeded
/// admin user (resolved per invocation via IIdentityUserRepository).
///
/// Re-entrancy: ExecuteDecisionAsync flips the decision to Executed via
/// AppDecisionLog.MarkExecuted, which does NOT publish DecisionMadeEto (only the
/// AppDecisionLog constructor does), so this handler can never trigger itself —
/// there is no event loop.
///
/// Failure isolation: the entire body is wrapped in try/catch and never rethrows.
/// The local distributed event bus delivers this event inside the unit of work
/// that recorded the decision (i.e. the scanner / stock transaction), and a
/// rethrown exception would roll that transaction back.
/// </summary>
public class DecisionAutopilotHandler : IDistributedEventHandler<DecisionMadeEto>, ITransientDependency
{
    /// <summary>ABP's identity seeder creates the admin user as "admin" (normalized "ADMIN").</summary>
    private const string AdminNormalizedUserName = "ADMIN";

    /// <summary>
    /// Seeded admin role (IdentityDataSeedContributor.AdminRoleName in the DbMigrator —
    /// not referenced from here, hence the literal). The role is granted every
    /// permission across all three modules, which covers everything this handler
    /// calls (DecisionLogs.Acknowledge, Rules.Default, PurchaseOrders.*, Transfers.*).
    /// </summary>
    private const string AdminRoleName = "admin";

    private readonly IDecisionLogAppService _decisionLogAppService;
    private readonly IInventoryRuleAppService _inventoryRuleAppService;
    private readonly IDecisionActionAppService _decisionActionAppService;
    private readonly IPurchaseOrderAppService _purchaseOrderAppService;
    private readonly IStockTransferAppService _stockTransferAppService;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly ILogger<DecisionAutopilotHandler> _logger;

    public DecisionAutopilotHandler(
        IDecisionLogAppService decisionLogAppService,
        IInventoryRuleAppService inventoryRuleAppService,
        IDecisionActionAppService decisionActionAppService,
        IPurchaseOrderAppService purchaseOrderAppService,
        IStockTransferAppService stockTransferAppService,
        IIdentityUserRepository identityUserRepository,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        ILogger<DecisionAutopilotHandler> logger)
    {
        _decisionLogAppService = decisionLogAppService;
        _inventoryRuleAppService = inventoryRuleAppService;
        _decisionActionAppService = decisionActionAppService;
        _purchaseOrderAppService = purchaseOrderAppService;
        _stockTransferAppService = stockTransferAppService;
        _identityUserRepository = identityUserRepository;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _logger = logger;
    }

    public async Task HandleEventAsync(DecisionMadeEto eventData)
    {
        try
        {
            var adminPrincipal = await BuildAdminPrincipalAsync();
            if (adminPrincipal == null)
            {
                _logger.LogWarning(
                    "Decision autopilot skipped for decision {DecisionLogId}: seeded admin user not found.",
                    eventData.DecisionLogId);
                return;
            }

            using (_currentPrincipalAccessor.Change(adminPrincipal))
            {
                await RunAutopilotAsync(eventData);
            }
        }
        catch (Exception ex)
        {
            // Never rethrow — a failing autopilot must not roll back the
            // scanner/stock transaction that published this event.
            _logger.LogError(
                ex,
                "Decision autopilot failed for decision {DecisionLogId} ({DecisionType}).",
                eventData.DecisionLogId,
                eventData.DecisionType);
        }
    }

    private async Task RunAutopilotAsync(DecisionMadeEto eventData)
    {
        var decision = await _decisionLogAppService.GetAsync(eventData.DecisionLogId);

        InventoryRuleDto rule;
        try
        {
            rule = await _inventoryRuleAppService.GetAsync(decision.RuleId);
        }
        catch (EntityNotFoundException)
        {
            // Rule deleted since the decision was recorded — nothing to automate.
            return;
        }

        // SuggestOnly (or any unknown/legacy value) = today's behavior: leave Pending.
        if (rule.ActionMode != RuleActionModes.CreateDraft && rule.ActionMode != RuleActionModes.AutoSubmit)
        {
            return;
        }

        // Only decision types that produce a corrective document run on autopilot.
        // ExcessStockAlert / DeadStockFlag stay Pending for a human even on autopilot.
        // WasteWriteOff is deliberately ABSENT from this list and must never be
        // added: executing it destroys stock (an irreversible WriteOff adjustment,
        // not a draft document a human later approves), so it is HUMAN-ONLY — a
        // manager executes it from the decision log, whatever the rule's ActionMode.
        var createsDocument = decision.DecisionType
            is DecisionTypes.LowStockAlert
            or DecisionTypes.ReorderSuggestion
            or DecisionTypes.StockoutRisk
            or DecisionTypes.TransferSuggestion;

        if (!createsDocument)
        {
            return;
        }

        // Race guard: another handler/user may already have resolved it.
        if (decision.Status != DecisionLogStatuses.Pending)
        {
            return;
        }

        var result = await _decisionActionAppService.ExecuteDecisionAsync(decision.Id);

        var submitted = false;
        if (rule.ActionMode == RuleActionModes.AutoSubmit && result.ActionCreated && result.ActionId.HasValue)
        {
            switch (result.ActionType)
            {
                case DecisionActionTypes.PurchaseOrder:
                    await _purchaseOrderAppService.SubmitAsync(result.ActionId.Value);
                    submitted = true;
                    break;
                case DecisionActionTypes.StockTransfer:
                    await _stockTransferAppService.SubmitAsync(result.ActionId.Value);
                    submitted = true;
                    break;
            }
        }

        _logger.LogInformation(
            "Decision autopilot ({ActionMode}) executed decision {DecisionLogId} ({DecisionType}): " +
            "created {ActionType} {ActionNumber}{Submitted}.",
            rule.ActionMode,
            decision.Id,
            decision.DecisionType,
            result.ActionType ?? "(no document)",
            result.ActionNumber ?? "-",
            submitted ? ", submitted for approval" : "");
    }

    /// <summary>
    /// Builds an authenticated ClaimsPrincipal for the seeded admin user so the
    /// [Authorize]-guarded app services called above pass their permission checks
    /// (the role claim feeds ABP's RolePermissionValueProvider). Returns null if
    /// the admin user does not exist.
    /// </summary>
    private async Task<ClaimsPrincipal?> BuildAdminPrincipalAsync()
    {
        var admin = await _identityUserRepository.FindByNormalizedUserNameAsync(AdminNormalizedUserName);
        if (admin == null)
        {
            return null;
        }

        // The authenticationType argument matters: without it IsAuthenticated is
        // false and every [Authorize] check would fail outright.
        var identity = new ClaimsIdentity("DecisionAutopilot");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, admin.Id.ToString()));
        identity.AddClaim(new Claim(AbpClaimTypes.UserName, admin.UserName));
        if (!string.IsNullOrWhiteSpace(admin.Email))
        {
            identity.AddClaim(new Claim(AbpClaimTypes.Email, admin.Email));
        }
        identity.AddClaim(new Claim(AbpClaimTypes.Role, AdminRoleName));

        return new ClaimsPrincipal(identity);
    }
}
