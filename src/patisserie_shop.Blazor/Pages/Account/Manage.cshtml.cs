using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using patisserie_shop.Localization;
using Volo.Abp.Account;
using Volo.Abp.AspNetCore.ExceptionHandling;

namespace patisserie_shop.Blazor.Pages.Account;

// Overrides the ABP Account module's /Account/Manage page with a warm-themed,
// self-contained version backed directly by IProfileAppService. ABP's own auth,
// validation and password rules are preserved — only the UI shell is replaced.
[Authorize]
public class ManageModel : PageModel
{
    private readonly IProfileAppService _profileAppService;
    private readonly IStringLocalizer<patisserie_shopResource> _localizer;
    private readonly ILogger<ManageModel> _logger;
    private readonly IExceptionToErrorInfoConverter _exceptionToErrorInfoConverter;

    public ManageModel(
        IProfileAppService profileAppService,
        IStringLocalizer<patisserie_shopResource> localizer,
        ILogger<ManageModel> logger,
        IExceptionToErrorInfoConverter exceptionToErrorInfoConverter)
    {
        _profileAppService = profileAppService;
        _localizer = localizer;
        _logger = logger;
        _exceptionToErrorInfoConverter = exceptionToErrorInfoConverter;
    }

    [BindProperty]
    public ProfileInfoModel ProfileInfo { get; set; } = new();

    [BindProperty]
    public PasswordInfoModel PasswordInfo { get; set; } = new();

    public bool HasPassword { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? StatusSection { get; set; }

    public async Task OnGetAsync()
    {
        await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        var profile = await _profileAppService.GetAsync();
        HasPassword = profile.HasPassword;

        ProfileInfo.UserName = profile.UserName;
        ProfileInfo.Email = profile.Email;
        ProfileInfo.Name = profile.Name;
        ProfileInfo.Surname = profile.Surname;
        ProfileInfo.PhoneNumber = profile.PhoneNumber;
        ProfileInfo.ConcurrencyStamp = profile.ConcurrencyStamp;
    }

    public async Task<IActionResult> OnPostProfileAsync()
    {
        // Each section posts its own form; validate only the profile sub-model so the
        // password section's [Required] rules don't block a profile-only save.
        ModelState.Clear();
        TryValidateModel(ProfileInfo, nameof(ProfileInfo));

        if (!ModelState.IsValid)
        {
            await RefreshHasPasswordAsync();
            return Page();
        }

        try
        {
            await _profileAppService.UpdateAsync(new UpdateProfileDto
            {
                UserName = ProfileInfo.UserName,
                Email = ProfileInfo.Email,
                Name = ProfileInfo.Name,
                Surname = ProfileInfo.Surname,
                PhoneNumber = ProfileInfo.PhoneNumber,
                ConcurrencyStamp = ProfileInfo.ConcurrencyStamp
            });

            StatusMessage = _localizer["Account:ProfileUpdated"].Value;
            StatusSection = "profile";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            AddSafeModelError(ex, "updating the current user's profile");
            await RefreshHasPasswordAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        // Profile fields are not posted by the password form; ignore their validation here.
        ModelState.Clear();
        TryValidateModel(PasswordInfo, nameof(PasswordInfo));
        if (!string.Equals(PasswordInfo.NewPassword, PasswordInfo.NewPasswordConfirm, StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                $"{nameof(PasswordInfo)}.{nameof(PasswordInfo.NewPasswordConfirm)}",
                _localizer["Account:PasswordMismatch"]);
        }

        if (!ModelState.IsValid)
        {
            await LoadProfileAsync();
            return Page();
        }

        try
        {
            await _profileAppService.ChangePasswordAsync(new ChangePasswordInput
            {
                CurrentPassword = PasswordInfo.CurrentPassword,
                NewPassword = PasswordInfo.NewPassword!
            });

            StatusMessage = _localizer["Account:PasswordChanged"].Value;
            StatusSection = "password";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            AddSafeModelError(ex, "changing the current user's password");
            await LoadProfileAsync();
            return Page();
        }
    }

    private void AddSafeModelError(Exception exception, string operation)
    {
        _logger.LogError(exception, "An error occurred while {Operation}.", operation);

        var errorInfo = _exceptionToErrorInfoConverter.Convert(exception, _ => { });
        var fallbackMessage = _localizer["Account:UnexpectedError"].Value;
        var message = !string.IsNullOrWhiteSpace(errorInfo?.Code)
            ? errorInfo.Message ?? fallbackMessage
            : fallbackMessage;

        ModelState.AddModelError(string.Empty, message);
    }

    private async Task RefreshHasPasswordAsync()
    {
        // Keep the user's edited profile values for redisplay; only refresh the
        // password-availability flag used to label the change-password section.
        var profile = await _profileAppService.GetAsync();
        HasPassword = profile.HasPassword;
    }

    public class ProfileInfoModel
    {
        [Display(Name = "Account:UserName")]
        [Required]
        [StringLength(256)]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "Account:Email")]
        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Account:Name")]
        [StringLength(64)]
        public string? Name { get; set; }

        [Display(Name = "Account:Surname")]
        [StringLength(64)]
        public string? Surname { get; set; }

        [Display(Name = "Account:PhoneNumber")]
        [StringLength(32)]
        public string? PhoneNumber { get; set; }

        public string? ConcurrencyStamp { get; set; }
    }

    public class PasswordInfoModel
    {
        [Display(Name = "Account:CurrentPassword")]
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [Display(Name = "Account:NewPassword")]
        [Required]
        [DataType(DataType.Password)]
        [StringLength(128, MinimumLength = 1)]
        public string? NewPassword { get; set; }

        [Display(Name = "Account:ConfirmNewPassword")]
        [DataType(DataType.Password)]
        public string? NewPasswordConfirm { get; set; }
    }
}
