using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;

namespace Umbrella.Wallet.App;

/// <summary>
/// Runtime colour themes.
///
/// Every themed surface binds a DynamicResource key, so swapping the palette repaints the whole
/// window without a restart. The QR code's white plate is deliberately NOT themed — a QR needs
/// dark-on-light contrast to scan, and tinting it would quietly break receiving.
/// </summary>
public static class Theming
{
    public sealed record ThemeOption(string Id, string Name);

    public static IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new("purple", "The fear · noir"),
        new("blue", "Blue"),
        new("green", "Green"),
        new("black", "Black (OLED)"),
        new("white", "White (light)"),
        new("gradient", "Gradient · violet"),
        new("sunset", "Gradient · sunset"),
        new("ember", "Ember · red"),
        new("crimson", "Crimson"),
        new("amber", "Amber"),
        new("slate", "Slate"),
    ];

    /// <summary>Order matters only for readability; every theme must define every key.</summary>
    private static readonly string[] Keys =
    [
        "UmBg", "UmBgAlt", "UmCard", "UmInput", "UmCardAlt", "UmHover",
        "UmBorder", "UmBorder2", "UmBorder3",
        "UmAccent", "UmAccentBright", "UmAccentHover", "UmAccentSel", "UmAccentDim",
        "UmText", "UmTextSoft", "UmTextDim", "UmTextMuted", "UmPos",
        "UmInverse", "UmInverseHover", "UmInverseText",
    ];

    private static readonly Dictionary<string, string[]> Palettes = new()
    {
        // bg       bgAlt    card     input    cardAlt  hover    bd       bd2      bd3      accent   accentBr accentHv accentSel accentDim text    textSoft textDim  textMut  pos
        // Primary "the fear" look: monochrome noir — near-black with cool white accents (the
        // FROSTFREED / reference mood). The accent is near-white, so accent-filled buttons read
        // white-on-black like the references. The red editorial look lives on as the Ember theme.
        ["purple"] =
        [
            "#050506", "#0C0D0F", "#131417", "#101114", "#1C1E22", "#24272C",
            "#1E2024", "#2A2D33", "#373B42",
            "#AEB6C2", "#EDF1F6", "#FFFFFF", "#2A2E36", "#22262C",
            "#F4F6F9", "#C4CBD4", "#8A929C", "#6C737C", "#7DCF8F",
            "#F4F6F9", "#FFFFFF", "#050506",
        ],
        // Electric-cyan glass on near-black — the neon-glass wallet mood.
        ["blue"] =
        [
            "#07090C", "#0C1016", "#111823", "#0E141C", "#16202E", "#1C2A3C",
            "#182430", "#23384A", "#2E4A63",
            "#1E6FA8", "#35C0FF", "#2AA0E0", "#143A54", "#123246",
            "#EAF4FB", "#B8CFDE", "#7E93A4", "#647686", "#6FD3A0",
            "#EAF4FB", "#FFFFFF", "#07090C",
        ],
        // Neon mint/emerald on near-black — the Neon Wallet mood.
        ["green"] =
        [
            "#06100B", "#0A1811", "#0F2016", "#0C1B13", "#16301F", "#1E4029",
            "#163024", "#22452F", "#2E5C3E",
            "#1E9E6A", "#43F5A5", "#35D08C", "#114A30", "#0F3A28",
            "#EBFBF1", "#C0DECB", "#86A692", "#6E8677", "#43F5A5",
            "#EBFBF1", "#FFFFFF", "#06100B",
        ],
        ["black"] =
        [
            "#000000", "#070707", "#0D0D0D", "#0A0A0A", "#141414", "#1E1E1E",
            "#1A1A1A", "#262626", "#333333",
            "#3A3A3A", "#8A8A8A", "#4A4A4A", "#2A2A2A", "#222222",
            "#FFFFFF", "#CFCFCF", "#9A9A9A", "#7A7A7A", "#7DCF8F",
            "#FFFFFF", "#E8E8E8", "#000000",
        ],
        // Light theme: text roles invert so contrast survives the flip.
        ["white"] =
        [
            "#F4F5F7", "#FFFFFF", "#FFFFFF", "#F0F1F4", "#E9EBF0", "#DFE3EC",
            "#DDE0E7", "#CBD0DA", "#B8BFCC",
            "#5B4CA8", "#6E5FB8", "#4C3F92", "#DAD5F2", "#C9C3E6",
            "#14161C", "#3A3F4B", "#5C6373", "#79808F", "#1E8E4A",
            "#1E2029", "#33374A", "#FFFFFF",
        ],
        // Vivid violet over a violet-black sweep — the purple wallet mood.
        ["gradient"] =
        [
            "#0E0A1C", "#140F26", "#1B1533", "#181229", "#251C46", "#2E2358",
            "#2A2148", "#382C63", "#443673",
            "#7C4DD6", "#B06BFF", "#9457E6", "#4B2F8E", "#402C6E",
            "#F6F3FB", "#CFC7DE", "#948CA6", "#7C748E", "#6FD3C5",
            "#F6F3FB", "#FFFFFF", "#0E0A1C",
        ],
        ["sunset"] =
        [
            "#1A0E14", "#221219", "#2B1720", "#26141C", "#3A1F2B", "#4A2836",
            "#3A2029", "#4E2C38", "#5F3746",
            "#A6455C", "#E8766A", "#C25A62", "#7E3446", "#6B3140",
            "#FBF2F3", "#E0C6C7", "#A98D93", "#8E757B", "#F0A05A",
            "#FBF2F3", "#FFFFFF", "#1A0E14",
        ],
        // Editorial noir with a hot red-orange accent — the mood of the reference posters.
        ["ember"] =
        [
            "#0B0A0A", "#131010", "#1B1514", "#171211", "#2A1E1A", "#33221C",
            "#2A201D", "#3A2A24", "#47332B",
            "#8A3320", "#FF4A24", "#C43E20", "#5C2417", "#4A241A",
            "#F6F1EF", "#D8C7C0", "#9A8A84", "#847670", "#7DCF8F",
            "#F6F1EF", "#FFFFFF", "#0B0A0A",
        ],
        ["crimson"] =
        [
            "#120809", "#1A0C0E", "#221012", "#1D0D0F", "#301619", "#3E1D21",
            "#2C1417", "#3D1D21", "#4C252A",
            "#8E2B33", "#D14A55", "#B03B45", "#6C2027", "#5A1F25",
            "#FBF1F2", "#DEC2C4", "#A78A8D", "#8C7174", "#7DCF8F",
            "#FBF1F2", "#FFFFFF", "#120809",
        ],
        ["amber"] =
        [
            "#120E05", "#1A1409", "#22190B", "#1D1509", "#312414", "#402F1A",
            "#2C2112", "#3D2E19", "#4C3A20",
            "#8A6417", "#D9A227", "#B4841F", "#6A4C11", "#59410F",
            "#FCF7EC", "#E0D2B4", "#A89673", "#8C7F63", "#7DCF8F",
            "#FCF7EC", "#FFFFFF", "#120E05",
        ],
        // Teal cyber on gunmetal — the tech/HUD mood.
        ["slate"] =
        [
            "#0A0C0D", "#0F1315", "#141A1C", "#111618", "#1B2528", "#243236",
            "#182123", "#243234", "#304446",
            "#1B8C7E", "#2DE0C0", "#23BEA6", "#124038", "#0F3730",
            "#EAF6F4", "#C0D4D0", "#849792", "#6C7E7A", "#2DE0C0",
            "#EAF6F4", "#FFFFFF", "#0A0C0D",
        ],
    };

    /// <summary>Gradient themes paint the page as a sweep instead of a flat fill.</summary>
    private static IBrush GradientBackground(string id)
    {
        var stops = id == "sunset"
            ? [("#2A0F1B", 0.0), ("#1C1020", 0.5), ("#120C18", 1.0)]
            : new[] { ("#160B2E", 0.0), ("#0D1230", 0.55), ("#08202B", 1.0) };

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        };
        foreach (var (colour, offset) in stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.Parse(colour), offset));
        }

        return brush;
    }

    public static string Current { get; private set; } = "purple";

    /// <summary>Light themes need dark artwork; the solid-white logo would vanish.</summary>
    public static bool IsLightTheme(string id) => id == "white";

    public static bool IsKnown(string id) => Palettes.ContainsKey(id);

    public static void Apply(string id)
    {
        if (!Palettes.TryGetValue(id, out var palette)) return;
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        for (var i = 0; i < Keys.Length; i++)
        {
            resources[Keys[i]] = new SolidColorBrush(Color.Parse(palette[i]));
        }

        if (id is "gradient" or "sunset") resources["UmBg"] = GradientBackground(id);

        // Card sheen: a GradientStop binds a Color, not a Brush, so these are published separately.
        // Derived from the card colour so every theme keeps the same subtle top-down lift.
        var card = Color.Parse(palette[Array.IndexOf(Keys, "UmCard")]);
        resources["UmCardTop"] = Lighten(card, id == "white" ? 1.0 : 1.10);
        resources["UmCardBottom"] = Lighten(card, id == "white" ? 0.985 : 0.90);

        // Readable text colour ON the accent, chosen by the accent's brightness — so accent-filled
        // buttons stay legible whether the theme accent is dark (red/violet) or light (mint/cyan).
        var accent = Color.Parse(palette[Array.IndexOf(Keys, "UmAccentBright")]);
        resources["UmAccentText"] = new SolidColorBrush(
            Luminance(accent) > 0.62 ? Color.Parse("#0A0A0B") : Colors.White);
        // A soft translucent wash of the accent, for hover fills and glows that follow the theme.
        resources["UmAccentWash"] = new SolidColorBrush(accent) { Opacity = 0.16 };

        Current = id;
    }

    /// <summary>Perceptual-ish brightness in 0..1, to decide dark-vs-light text on a colour.</summary>
    private static double Luminance(Color c) => ((0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B)) / 255.0;

    /// <summary>Scales a colour's channels, clamped so bright themes don't wrap around to black.</summary>
    private static Color Lighten(Color colour, double factor) => Color.FromRgb(
        (byte)Math.Clamp(colour.R * factor, 0, 255),
        (byte)Math.Clamp(colour.G * factor, 0, 255),
        (byte)Math.Clamp(colour.B * factor, 0, 255));

    /// <summary>Seeds the default palette before the first window is shown.</summary>
    public static void ApplyDefaults() => Apply(Current);

    public static string NameOf(string id) =>
        Themes.FirstOrDefault(t => t.Id == id)?.Name ?? id;
}
