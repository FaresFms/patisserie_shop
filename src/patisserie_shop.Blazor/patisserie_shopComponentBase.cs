using System;
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
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Volo.Abp.Authorization;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.AspNetCore.Components;
using Volo.Abp.Settings;
using Volo.Abp.Validation;

namespace patisserie_shop.Blazor;

public abstract class patisserie_shopComponentBase : AbpComponentBase
{
    [Inject] protected IDialogService DialogService { get; set; } = null!;
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

        await base.HandleErrorAsync(ex);
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
