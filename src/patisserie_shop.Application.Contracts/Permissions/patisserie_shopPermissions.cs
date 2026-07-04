namespace patisserie_shop.Permissions;

public static class patisserie_shopPermissions
{
    public const string GroupName = "patisserie_shop";

    /// <summary>
    /// Shop-wide settings (branding, receipt, operational defaults). Only the
    /// shop owner / admin should change these, so the page and the write side of
    /// <c>IShopSettingsAppService</c> require <see cref="Settings.Manage"/>.
    /// </summary>
    public static class Settings
    {
        public const string Default = GroupName + ".Settings";
        public const string Manage = Default + ".Manage";
    }
}
