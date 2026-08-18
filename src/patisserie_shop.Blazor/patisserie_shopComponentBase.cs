using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MudBlazor;
using Inventory.Settings;
using patisserie_shop.Blazor.Shared.Components.SoftComponents;
using patisserie_shop.Localization;
using patisserie_shop.Settings;
using Intelligence.Localization;
using Inventory.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Operations.Localization;
using Production.Localization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.AspNetCore.Components;
using Volo.Abp.Settings;
using Volo.Abp.Validation;

namespace patisserie_shop.Blazor;

public abstract class patisserie_shopComponentBase : AbpComponentBase
{
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [Inject] protected ISnackbar AppSnackbar { get; set; } = null!;
    [Inject] protected IStringLocalizerFactory StringLocalizerFactory { get; set; } = null!;
    [Inject] protected ISettingProvider AppSettingProvider { get; set; } = null!;
    [Inject] protected IExceptionToErrorInfoConverter ExceptionToErrorInfoConverter { get; set; } = null!;
    [Inject] protected ILogger<patisserie_shopComponentBase> AppLogger { get; set; } = null!;
    [Inject] protected NavigationManager AppNavigationManager { get; set; } = null!;

    protected string ShopCurrency { get; private set; } = ShopCurrencySettings.Fallback;

    protected patisserie_shopComponentBase()
    {
        LocalizationResource = typeof(patisserie_shopResource);
    }

    protected override async Task HandleErrorAsync(Exception ex)
    {
        if (IsAuthorizationFailure(ex))
        {
            AppLogger.LogWarning(ex, "Authorization failed while rendering {Uri}", AppNavigationManager.Uri);
            AppNavigationManager.NavigateTo("/Account/AccessDenied", forceLoad: true);
            return;
        }

        if (TryGetValidationErrors(ex, out var errors))
        {
            await ShowValidationErrorsAsync(errors);
            return;
        }

        // Domain rules ("not scheduled yet", "not enough stock") are things the shop
        // owner can act on, so they must always be visible. The base handler routes
        // them to a theme dialog that never renders over this app's hand-rolled
        // glass-modal overlays, which made failed actions look like dead buttons.
        if (FindBusinessException(ex) is { } businessEx)
        {
            AppLogger.LogWarning(businessEx, "A business rule blocked the action.");
            var message = GetBusinessRuleMessage(businessEx);
            // Raised from an event handler that may be off the renderer's sync context,
            // so hop back onto it — otherwise the toast is queued but never painted.
            await InvokeAsync(() =>
            {
                AppSnackbar.Add(message, Severity.Warning);
                StateHasChanged();
            });
            return;
        }

        await base.HandleErrorAsync(ex);
    }

    /// <summary>
    /// Resolves a domain rule to its shop-owner-facing sentence, filling in any
    /// {Placeholders} the rule attached via WithData.
    /// </summary>
    protected string GetBusinessRuleMessage(BusinessException ex)
    {
        var text = LocalizeErrorCode(ex);
        if (text is not null)
        {
            return ApplyExceptionData(text, ex);
        }

        // Never surface raw exception text: an unlocalized BusinessException carries
        // .NET's "Exception of type '...' was thrown." message, which is meaningless
        // to a shop owner.
        return LooksLikeRawExceptionText(ex.Message)
            ? L["Error:UnexpectedTryAgain"].Value
            : ex.Message;
    }

    /// <summary>
    /// Resolves an error code to its message from the module resource that owns the
    /// code namespace ("Operations:Transfers:InsufficientStock" -> OperationsResource).
    /// Tries the bare code first (ABP's own convention, used by the Operations,
    /// Inventory and most Production entries) then the "Error:{code}" form that a
    /// handful of Production entries use.
    /// ABP's error-info converter is deliberately not used: inside the Blazor circuit
    /// it returns its generic "internal error" sentence instead of the rule text.
    /// </summary>
    private string? LocalizeErrorCode(BusinessException ex)
    {
        if (string.IsNullOrWhiteSpace(ex.Code))
        {
            return null;
        }

        string[] keys = [ex.Code!, $"Error:{ex.Code}"];
        var resourceType = ResourceTypeForCode(ex.Code!);

        if (resourceType != null)
        {
            var moduleLocalizer = StringLocalizerFactory.Create(resourceType);
            foreach (var key in keys)
            {
                var moduleText = moduleLocalizer[key];
                if (!moduleText.ResourceNotFound)
                {
                    return moduleText.Value;
                }
            }
        }

        foreach (var key in keys)
        {
            var appText = L[key];
            if (!appText.ResourceNotFound)
            {
                return appText.Value;
            }
        }

        return null;
    }

    /// <summary>True for .NET's default "Exception of type 'X' was thrown." filler.</summary>
    private static bool LooksLikeRawExceptionText(string? message)
        => string.IsNullOrWhiteSpace(message)
           || (message.StartsWith("Exception of type", StringComparison.Ordinal)
               && message.EndsWith("was thrown.", StringComparison.Ordinal));

    /// <summary>Maps an error code's namespace to the module resource that defines it.</summary>
    private static Type? ResourceTypeForCode(string code) => code.Split(':')[0] switch
    {
        "Production" => typeof(ProductionResource),
        "Operations" => typeof(OperationsResource),
        "Inventory" => typeof(InventoryResource),
        "Intelligence" => typeof(IntelligenceResource),
        _ => null
    };

    /// <summary>Replaces {Name} placeholders with the values the rule attached.</summary>
    private static string ApplyExceptionData(string text, Exception ex)
    {
        if (ex.Data.Count == 0)
        {
            return text;
        }

        foreach (DictionaryEntry entry in ex.Data)
        {
            if (entry.Key is string name)
            {
                text = text.Replace($"{{{name}}}", entry.Value?.ToString() ?? string.Empty, StringComparison.Ordinal);
            }
        }

        return text;
    }

    /// <summary>Unwraps to the first <see cref="BusinessException"/> in the chain, if any.</summary>
    protected static BusinessException? FindBusinessException(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is BusinessException businessEx)
            {
                return businessEx;
            }

            ex = ex.InnerException;
        }

        return null;
    }

    protected async Task LoadShopCurrencyAsync()
    {
        var currency = await AppSettingProvider.GetOrNullAsync(patisserie_shopSettings.DefaultCurrency);
        ShopCurrency = ShopCurrencySettings.Normalize(currency);
    }

    protected string FormatShopMoney(decimal value, int decimals = 2)
        => $"{value.ToString($"N{decimals}", CultureInfo.CurrentCulture)} {ShopCurrency}";

    protected string GetFriendlyErrorMessage(
        Exception ex,
        string fallbackResourceKey = "Error:UnexpectedTryAgain")
    {
        AppLogger.LogError(ex, "A user-facing operation failed.");

        var errorInfo = ExceptionToErrorInfoConverter.Convert(ex, includeSensitiveDetails: false);
        if (!string.IsNullOrWhiteSpace(errorInfo.Code) &&
            !string.IsNullOrWhiteSpace(errorInfo.Message))
        {
            return errorInfo.Message;
        }

        return L[fallbackResourceKey].Value;
    }

    protected async Task ShowValidationErrorsAsync(IReadOnlyList<ValidationErrorDialog.ValidationErrorItem> errors)
    {
        var parameters = new DialogParameters
        {
            [nameof(ValidationErrorDialog.Errors)] = errors,
            [nameof(ValidationErrorDialog.Title)] = L["ValidationErrorsTitle"].Value,
            [nameof(ValidationErrorDialog.IntroText)] = L["ValidationErrorsIntro"].Value,
            [nameof(ValidationErrorDialog.CloseText)] = L["OK"].Value
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            CloseOnEscapeKey = true,
            BackdropClick = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        await DialogService.ShowAsync<ValidationErrorDialog>(null, parameters, options);
    }

    private bool TryGetValidationErrors(
        Exception ex,
        out IReadOnlyList<ValidationErrorDialog.ValidationErrorItem> errors)
    {
        var validationEx = FindValidationException(ex);
        if (validationEx == null || validationEx.ValidationErrors == null || validationEx.ValidationErrors.Count == 0)
        {
            errors = Array.Empty<ValidationErrorDialog.ValidationErrorItem>();
            return false;
        }

        errors = validationEx.ValidationErrors
            .Select(r => new ValidationErrorDialog.ValidationErrorItem(
                FormatFieldName(r),
                string.IsNullOrWhiteSpace(r.ErrorMessage)
                    ? L["Validation:InvalidValue"].Value
                    : r.ErrorMessage!))
            .ToList();

        return true;
    }

    private static AbpValidationException? FindValidationException(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is AbpValidationException abp) return abp;
            ex = ex.InnerException;
        }
        return null;
    }

    private static bool IsAuthorizationFailure(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is AbpAuthorizationException or UnauthorizedAccessException)
            {
                return true;
            }

            ex = ex.InnerException;
        }

        return false;
    }

    private static string? FormatFieldName(ValidationResult result)
    {
        var name = result.MemberNames?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(name)) return null;

        // Strip indexer prefixes like "Items[0]." → leave just the leaf field name
        var dotIdx = name.LastIndexOf('.');
        if (dotIdx >= 0 && dotIdx < name.Length - 1)
        {
            name = name[(dotIdx + 1)..];
        }
        return name;
    }
}
