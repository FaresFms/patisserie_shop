using MudBlazor;

namespace patisserie_shop.Blazor.Shared.Themes.MudTheme
{
    public static class MyTheme
    {
        public static global::MudBlazor.MudTheme Create()
        {
            var theme = new global::MudBlazor.MudTheme
            {
                // ═══════════════════════════════
                //  PALETTE — Light Mode (Modern FinTech)
                // ═══════════════════════════════
                PaletteLight = new PaletteLight
                {
                    Primary = "#2563EB",           // Royal Blue - Trust & Security
                    PrimaryDarken = "#1D4ED8",
                    PrimaryLighten = "#DBEAFE",
                    PrimaryContrastText = "#FFFFFF",

                    Secondary = "#10B981",         // Emerald Green - Money/Success
                    SecondaryDarken = "#059669",
                    SecondaryContrastText = "#FFFFFF",

                    Tertiary = "#334155",          // Slate for neutral accents

                    Background = "#F8FAFC",        // Very soft cool gray
                    Surface = "#FFFFFF",
                    DrawerBackground = "#FFFFFF",
                    AppbarBackground = "#0F172A",  // Slate 900 - Dark sleek premium header
                    AppbarText = "#F8FAFC",

                    TextPrimary = "#0F172A",
                    TextSecondary = "#475569",
                    TextDisabled = "#94A3B8",

                    ActionDefault = "#475569",
                    Divider = "#E2E8F0",
                    DividerLight = "#F1F5F9",

                    Success = "#059669",
                    Error = "#E11D48",
                    Warning = "#D97706",
                    Info = "#0284C7",

                    OverlayDark = "rgba(15,23,42,0.5)", // Darker, sleeker overlay
                },

                // ═══════════════════════════════
                //  PALETTE — Dark Mode (Sleek FinTech)
                // ═══════════════════════════════
                PaletteDark = new PaletteDark
                {
                    Primary = "#3B82F6",
                    PrimaryDarken = "#2563EB",
                    PrimaryLighten = "#93C5FD",
                    PrimaryContrastText = "#FFFFFF",

                    Secondary = "#34D399",
                    SecondaryDarken = "#10B981",
                    SecondaryContrastText = "#0F172A",

                    Tertiary = "#94A3B8",

                    Background = "#0F172A",        // Deep Slate
                    Surface = "#1E293B",
                    DrawerBackground = "#1E293B",
                    AppbarBackground = "#1E293B",
                    AppbarText = "#F8FAFC",

                    TextPrimary = "#F8FAFC",
                    TextSecondary = "#CBD5E1",
                    TextDisabled = "#64748B",

                    ActionDefault = "#94A3B8",
                    Divider = "#334155",
                    DividerLight = "#1E293B",

                    Success = "#10B981",
                    Error = "#F43F5E",
                    Warning = "#F59E0B",
                    Info = "#38BDF8",

                    OverlayDark = "rgba(0,0,0,0.7)",
                },

                // ═══════════════════════════════
                //  TYPOGRAPHY
                // ═══════════════════════════════
                Typography = new Typography
                {
                    Default = new DefaultTypography
                    {
                        FontFamily = new[] { "Inter", "Roboto", "Segoe UI", "sans-serif" }, // Inter is great for dashboards
                        FontSize = "0.875rem",
                        FontWeight = "400",
                        LineHeight = "1.5",
                        LetterSpacing = "0.01em"
                    },
                    H1 = new H1Typography { FontSize = "3rem", FontWeight = "600", LineHeight = "1.2" },
                    H2 = new H2Typography { FontSize = "2.25rem", FontWeight = "600" },
                    H3 = new H3Typography { FontSize = "1.875rem", FontWeight = "600" },
                    H4 = new H4Typography { FontSize = "1.5rem", FontWeight = "600" },
                    H5 = new H5Typography { FontSize = "1.25rem", FontWeight = "600" },
                    H6 = new H6Typography { FontSize = "1rem", FontWeight = "600" },
                    Subtitle1 = new Subtitle1Typography { FontSize = "1rem", FontWeight = "500", LetterSpacing = "0.01em" },
                    Subtitle2 = new Subtitle2Typography { FontSize = "0.875rem", FontWeight = "500" },
                    Body1 = new Body1Typography { FontSize = "1rem", FontWeight = "400", LineHeight = "1.5" },
                    Body2 = new Body2Typography { FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.43" },
                    Button = new ButtonTypography
                    {
                        FontSize = "0.875rem",
                        FontWeight = "600",
                        LetterSpacing = "0.02em",
                        TextTransform = "none" // Capitalized looks cleaner than all upper
                    },
                    Caption = new CaptionTypography { FontSize = "0.75rem", FontWeight = "500" },
                },

                // ═══════════════════════════════
                //  SHAPE — Sleek Rounded
                // ═══════════════════════════════
                LayoutProperties = new LayoutProperties
                {
                    DefaultBorderRadius = "8px",   // Crisp modern corners
                    DrawerWidthLeft = "260px",
                    DrawerWidthRight = "260px",
                    AppbarHeight = "64px",
                },

                ZIndex = new ZIndex
                {
                    Drawer = 1200,
                    AppBar = 1100,
                    Dialog = 1300,
                    Snackbar = 1400,
                    Tooltip = 1500,
                }
            };

            // Soft elevation similar to Tailwind / modern web shadows
            theme.Shadows.Elevation[0] = "none";
            theme.Shadows.Elevation[1] = "0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06)";
            theme.Shadows.Elevation[2] = "0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)";
            theme.Shadows.Elevation[3] = "0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)";
            theme.Shadows.Elevation[4] = "0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)";
            theme.Shadows.Elevation[5] = "0 25px 50px -12px rgba(0, 0, 0, 0.25)";

            return theme;
        }
    }
}