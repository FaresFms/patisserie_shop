using System.Collections.Generic;
using System.Security.Claims;
using Inventory;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;

namespace patisserie_shop.DbMigrator;

/// <summary>
/// The single source of truth for the graduation-presentation identities and branches.
/// Usernames stay intentionally short so accounts can be entered quickly on stage, while
/// the profile names and branch data look like a real operating business.
/// </summary>
internal static class GraduationSeedData
{
    public const string Password = "Admin@2026";
    public const string MainKitchenName = "المطبخ المركزي";

    public static IReadOnlyList<BranchPresentationSpec> RetailBranches { get; } =
    [
        new(
            "فرع المزة",
            "manager",
            "cashier",
            "أحمد",
            "الخطيب",
            "نور",
            "الحسن",
            "أوتوستراد المزة، جانب مدينة الجلاء، دمشق",
            "+963-11-612-4101",
            "mazzeh@daralward.example",
            8,
            13),
        new(
            "فرع المالكي",
            "manager2",
            "cashier2",
            "رنا",
            "مراد",
            "سامر",
            "يوسف",
            "شارع عبد المنعم رياض، المالكي، دمشق",
            "+963-11-373-4202",
            "malki@daralward.example",
            7,
            12),
        new(
            "فرع أبو رمانة",
            "manager3",
            "cashier3",
            "كريم",
            "الدروبي",
            "ليان",
            "نصار",
            "جادة ناظم باشا، أبو رمانة، دمشق",
            "+963-11-333-4303",
            "abou-remmaneh@daralward.example",
            7,
            11),
        new(
            "فرع كفرسوسة",
            "manager4",
            "cashier4",
            "دانا",
            "الحموي",
            "مازن",
            "عباس",
            "شارع البراعم، كفرسوسة، دمشق",
            "+963-11-214-4404",
            "kafarsouseh@daralward.example",
            6,
            10),
        new(
            "فرع الشعلان",
            "manager5",
            "cashier5",
            "يوسف",
            "الرفاعي",
            "سارة",
            "بركات",
            "شارع الحمراء، الشعلان، دمشق",
            "+963-11-334-4505",
            "shaalaan@daralward.example",
            8,
            14),
        new(
            "فرع مشروع دمر",
            "manager6",
            "cashier6",
            "هبة",
            "الزعبي",
            "عمر",
            "سلوم",
            "الجزيرة السادسة، مشروع دمر، دمشق",
            "+963-11-313-4606",
            "dummar@daralward.example",
            5,
            9),
        new(
            "فرع باب توما",
            "manager7",
            "cashier7",
            "جورج",
            "حداد",
            "ميرا",
            "داود",
            "ساحة باب توما، دمشق القديمة",
            "+963-11-542-4707",
            "bab-touma@daralward.example",
            7,
            12),
        new(
            "فرع جرمانا",
            "manager8",
            "cashier8",
            "نادر",
            "منصور",
            "ريم",
            "إبراهيم",
            "شارع البلدية، جرمانا، ريف دمشق",
            "+963-11-563-4808",
            "jarmana@daralward.example",
            6,
            10)
    ];

    public static BranchPresentationSpec MainKitchen { get; } = new(
        MainKitchenName,
        "kitchen",
        CashierUserName: null,
        "فادي",
        "الشامي",
        CashierName: null,
        CashierSurname: null,
        "المنطقة الصناعية، مدخل دمشق الجنوبي",
        "+963-11-665-4000",
        "kitchen@daralward.example",
        0,
        0,
        BranchTypes.MainKitchen);

    public static IEnumerable<UserPresentationSpec> Users()
    {
        foreach (var branch in RetailBranches)
        {
            yield return new UserPresentationSpec(
                branch.ManagerUserName,
                $"{branch.ManagerUserName}@daralward.example",
                branch.ManagerName,
                branch.ManagerSurname,
                IdentityDataSeedContributor.BranchManagerRoleName);

            yield return new UserPresentationSpec(
                branch.CashierUserName!,
                $"{branch.CashierUserName}@daralward.example",
                branch.CashierName!,
                branch.CashierSurname!,
                IdentityDataSeedContributor.CashierRoleName);
        }

        yield return new UserPresentationSpec(
            MainKitchen.ManagerUserName,
            MainKitchen.Email,
            MainKitchen.ManagerName,
            MainKitchen.ManagerSurname,
            IdentityDataSeedContributor.KitchenManagerRoleName);
    }

    public static ClaimsPrincipal PrincipalFor(IdentityUser user, string roleName)
    {
        var identity = new ClaimsIdentity("GraduationPresentation");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, user.Id.ToString()));
        identity.AddClaim(new Claim(AbpClaimTypes.UserName, user.UserName));
        identity.AddClaim(new Claim(AbpClaimTypes.Role, roleName));
        return new ClaimsPrincipal(identity);
    }
}

internal sealed record UserPresentationSpec(
    string UserName,
    string Email,
    string Name,
    string Surname,
    string RoleName);

internal sealed record BranchPresentationSpec(
    string Name,
    string ManagerUserName,
    string? CashierUserName,
    string ManagerName,
    string ManagerSurname,
    string? CashierName,
    string? CashierSurname,
    string Address,
    string Phone,
    string Email,
    int MinSalesPerDay,
    int MaxSalesPerDay,
    string BranchType = BranchTypes.SalesBranch);
