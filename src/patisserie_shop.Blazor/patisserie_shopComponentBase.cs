using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using MudBlazor;
using patisserie_shop.Blazor.Shared.Components.SoftComponents;
using patisserie_shop.Localization;
using Microsoft.AspNetCore.Components;
using Volo.Abp.AspNetCore.Components;
using Volo.Abp.Validation;

namespace patisserie_shop.Blazor;

public abstract class patisserie_shopComponentBase : AbpComponentBase
{
    [Inject] protected IDialogService DialogService { get; set; } = null!;

    protected patisserie_shopComponentBase()
    {
        LocalizationResource = typeof(patisserie_shopResource);
    }

    protected override async Task HandleErrorAsync(Exception ex)
    {
        if (TryGetValidationErrors(ex, out var errors))
        {
            await ShowValidationErrorsAsync(errors);
            return;
        }

        await base.HandleErrorAsync(ex);
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

    private static bool TryGetValidationErrors(
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
                string.IsNullOrWhiteSpace(r.ErrorMessage) ? "Invalid value." : r.ErrorMessage!))
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
