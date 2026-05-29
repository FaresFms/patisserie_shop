using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Volo.Abp.Account;

namespace patisserie_shop.Blazor.Pages.Account;

// Overrides the ABP Account module's /Account/Manage page with a warm-themed,
// self-contained version backed directly by IProfileAppService. ABP's own auth,
// validation and password rules are preserved — only the UI shell is replaced.
[Authorize]
public class ManageModel : PageModel
{
    private readonly IProfileAppService _profileAppService;

    public ManageModel(IProfileAppService profileAppService)
    {
        _profileAppService = profileAppService;
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

            StatusMessage = "Your profile has been updated.";
            StatusSection = "profile";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await RefreshHasPasswordAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        // Profile fields are not posted by the password form; ignore their validation here.
        ModelState.Clear();
        TryValidateModel(PasswordInfo, nameof(PasswordInfo));

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

            StatusMessage = "Your password has been changed.";
            StatusSection = "password";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadProfileAsync();
            return Page();
        }
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
        [Required]
        [StringLength(256)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [StringLength(64)]
        public string? Name { get; set; }

        [StringLength(64)]
        public string? Surname { get; set; }

        [StringLength(32)]
        public string? PhoneNumber { get; set; }

        public string? ConcurrencyStamp { get; set; }
    }

    public class PasswordInfoModel
    {
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(128, MinimumLength = 1)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The new password and its confirmation do not match.")]
        public string? NewPasswordConfirm { get; set; }
    }
}
