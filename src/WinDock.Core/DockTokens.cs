namespace WinDock.Core;

/// <summary>Shared layout and color tokens for the Dockset-style island and in-bar pills.</summary>
public static class DockTokens
{
    /// <summary>Win11 window / flyout radius (8 epx at 96 DPI).</summary>
    public const double IslandCornerRadius = 8;
    public const double IslandPaddingX = 5;
    public const double IslandPaddingY = 5;
    public const double IslandMargin = 10;
    public const double MinIslandLength = 160;
    /// <summary>Win11 control radius (buttons, pills, inputs).</summary>
    public const double TileCornerRadius = 4;
    public const double TilePaddingX = 10;
    public const double TilePaddingY = 6;
    public const double TileHeight = 50;
    public const double TileGap = 3;
    public const double ArtCornerRadius = 4;
    public const double GlyphSize = 14;
    public const double PrimaryFontSize = 15;
    public const double SecondaryFontSize = 11;
    public const double ArtSize = 40;
    public const double ClockFaceSize = 26;
    public const double ClockPanelFaceSize = 84;
    public const double SparklineHeight = 14;
    public const double SparklineDockWidth = 40;
    public const double ResizeGrip = 8;
    public const double OverflowButtonWidth = 32;
    public const double OverflowButtonHeight = 32;
    public const double OverflowGlyphSize = 12;
    public const double OverflowButtonGap = 4;
    public const double FlyoutUserCopyMaxWidth = 248;
    public const double ScrollStep = 140;
    public const double AutoHideStrip = 4;
    public const int AutoHideIdleMs = 800;
    public const int HysteresisDelayMs = 300;
    public const int WakeHoldMs = 2000;
    public const double FlyoutOffset = 8;
    /// <summary>Extra pixels around the island when testing pointer approach into the hotzone.</summary>
    public const int TrapezoidBuffer = 8;
    public const int DefaultIslandTransparency = 50;
    public const int FrostedIslandTransparency = 0;
    public const int GlassIslandTransparency = 80;
    public const int MinIslandAlpha = 12;
    public const int MaxIslandAlpha = 220;

    public static int TransparencyFor(DockBackdrop backdrop) => backdrop switch
    {
        DockBackdrop.Frosted => FrostedIslandTransparency,
        DockBackdrop.Glass => GlassIslandTransparency,
        _ => DefaultIslandTransparency
    };

    public static class Dark
    {
        public const uint IslandFill = 0xFF2A2A38;
        public const uint IslandFrosted = 0xFF1A1A22;
        public const uint IslandGlass = 0xFF0C1018;
        public const uint IslandFallback = 0xF2141418;
        public const uint OverflowFill = 0xE632323A;
        public const uint TileFill = 0xF03A3A46;
        public const uint TileFrosted = 0xE6363640;
        public const uint TileGlass = 0xCC2E2E38;
        public const uint TileHover = 0x33FFFFFF;
        public const uint Stroke = 0x26FFFFFF;
        public const uint Text = 0xFFFFFFFF;
    }

    public static class Light
    {
        public const uint IslandFill = 0xFFF3EFE6;
        public const uint IslandFrosted = 0xFFE6EEF4;
        public const uint IslandGlass = 0xFFFFFFFF;
        public const uint IslandFallback = 0xE6F2F2F4;
        public const uint OverflowFill = 0xF2F1F5FA;
        public const uint TileFill = 0xF7FFFFFF;
        public const uint TileFrosted = 0xE6F2F4F8;
        public const uint TileGlass = 0xCCFFFFFF;
        public const uint TileHover = 0xFFFFFFFF;
        public const uint Stroke = 0x1A000000;
        public const uint Text = 0xFF1A1A1A;
    }

    public static uint ResolveIslandFill(bool isLight, bool backdropOk, DockBackdrop backdrop, int transparency = DefaultIslandTransparency)
    {
        uint baseFill;
        if (!backdropOk)
        {
            baseFill = isLight ? Light.IslandFallback : Dark.IslandFallback;
        }
        else
        {
            baseFill = backdrop switch
            {
                DockBackdrop.Glass => isLight ? Light.IslandGlass : Dark.IslandGlass,
                DockBackdrop.Frosted => isLight ? Light.IslandFrosted : Dark.IslandFrosted,
                _ => isLight ? Light.IslandFill : Dark.IslandFill
            };
        }

        var (_, r, g, b) = Split(baseFill);
        var t = EffectiveTransparency(backdrop, transparency);
        var alpha = (byte)(MinIslandAlpha + (MaxIslandAlpha - MinIslandAlpha) * (100 - t) / 100);
        return Pack(alpha, r, g, b);
    }

    public static uint ResolveMenuFill(bool isLight, bool backdropOk, DockBackdrop backdrop)
    {
        var (_, r, g, b) = Split(ResolveIslandFill(isLight, backdropOk, backdrop, 0));
        return Pack(255, r, g, b);
    }

    public static int EffectiveTransparency(DockBackdrop backdrop, int transparency)
    {
        var t = Math.Clamp(transparency, 0, 100);
        return backdrop switch
        {
            DockBackdrop.Mica => t * 55 / 100,
            DockBackdrop.Frosted => t * 78 / 100,
            _ => t
        };
    }

    public static uint ResolveTileFill(bool isLight, DockBackdrop backdrop)
    {
        return backdrop switch
        {
            DockBackdrop.Glass => isLight ? Light.TileGlass : Dark.TileGlass,
            DockBackdrop.Frosted => isLight ? Light.TileFrosted : Dark.TileFrosted,
            _ => isLight ? Light.TileFill : Dark.TileFill
        };
    }

    /// <summary>Opaque input well on a flyout. Alpha is always 255 so ClearType/layered popups keep glyphs visible.</summary>
    public static uint ResolveFieldFill(bool isLight, DockBackdrop backdrop = DockBackdrop.Mica)
    {
        var (_, r, g, b) = Split(ResolveMenuFill(isLight, true, backdrop));
        return isLight
            ? Pack(255, Scale(r, 0.90), Scale(g, 0.90), Scale(b, 0.90))
            : Pack(255, Lift(r, 0.16), Lift(g, 0.16), Lift(b, 0.16));
    }

    /// <summary>Secondary flyout buttons and chips. Same well as <see cref="ResolveFieldFill"/>.</summary>
    public static uint ResolveQuietFill(bool isLight, DockBackdrop backdrop = DockBackdrop.Mica) =>
        ResolveFieldFill(isLight, backdrop);

    private static byte Scale(byte c, double factor) => (byte)Math.Clamp(c * factor, 0, 255);

    private static byte Lift(byte c, double amount) => (byte)Math.Clamp(c + (255 - c) * amount, 0, 255);

    public const uint Cpu = 0xFFC084FC;
    public const uint Memory = 0xFF3B82F6;
    public const uint Network = 0xFF3B82F6;
    public const uint NetworkUp = 0xFFD946EF;
    public const uint BatteryOk = 0xFF34D399;
    public const uint BatteryLow = 0xFFFBBF24;
    public const uint BatteryCritical = 0xFFF87171;
    public const uint NotesLight = 0xF2FBF0C8;
    public const uint NotesDark = 0xE6C4A35A;
    public const uint Focus = 0xFFA78BFA;
    public const uint Bing = 0xFF38BDF8;
    public const uint Alarm = 0xFFFF9F0A;
    public const uint QuietFill = 0x14000000;
    public const uint Danger = 0xFFE11D48;

    /// <summary>Opaque settings / picker chrome. Do not reuse island backdrop fills here.</summary>
    public static class Settings
    {
        public const uint Accent = 0xFF2563EB;
        public const uint AccentHover = 0xFF1D4ED8;
        public const uint OnAccent = 0xFFFFFFFF;
        public const uint FocusRing = 0xFF2563EB;
        public const double ControlHeight = 32;
        public const double TabMinWidth = 96;
        public const double NavWidth = 168;
        public const double CatalogNavWidth = 168;
        public const double NavPad = 12;
        public const double CatalogCardWidth = 160;
        public const double CatalogCardGap = 16;
        public const double CatalogPreviewHeight = 96;
        public const double CatalogPreviewPad = 12;
        public const double FocusRingThickness = 2;
        public const double FieldGap = 8;
        public const double SectionGap = 24;
        public const double PagePad = 24;
        public const double ControlPad = 12;
        public const double ScrollThickness = 12;
        public const double ScrollThumbSize = 6;

        public static class Light
        {
            public const uint Page = 0xFFF4F4F6;
            public const uint Card = 0xFFFFFFFF;
            public const uint Stroke = 0x14000000;
            public const uint BorderStrong = 0x33000000;
            public const uint Text = 0xFF1A1A1A;
            public const uint Muted = 0xFF6F6F76;
            public const uint FgDisabled = 0xFFB0B0B5;
            public const uint NavPane = 0xFFF4F5F7;
            public const uint NavSelected = 0xFFE6E6EA;
            public const uint Catalog = 0xFFFFFFFF;
            public const uint Preview = 0xFFF1F2F4;
            public const uint Search = 0xFFE8E8EC;
            public const uint DangerSubtle = 0x14E11D48;
            public const uint ScrollThumb = 0x59000000;
            public const uint ScrollThumbHover = 0x8A000000;
            public const uint ScrollThumbPressed = 0xB3000000;
        }

        public static class Dark
        {
            public const uint Page = 0xFF1C1C1E;
            public const uint Card = 0xFF2C2C2E;
            public const uint Stroke = 0x33FFFFFF;
            public const uint BorderStrong = 0x66FFFFFF;
            public const uint Text = 0xFFF2F2F2;
            public const uint Muted = 0xFFB0B0B5;
            public const uint FgDisabled = 0xFF8E8E93;
            public const uint NavPane = 0xFF232326;
            public const uint NavSelected = 0xFF48484A;
            public const uint Catalog = 0xFF1C1C1E;
            public const uint Preview = 0xFF3A3A3C;
            public const uint Search = 0xFF3A3A3C;
            public const uint DangerSubtle = 0x33E11D48;
            public const uint ScrollThumb = 0x66FFFFFF;
            public const uint ScrollThumbHover = 0x99FFFFFF;
            public const uint ScrollThumbPressed = 0xCCFFFFFF;
        }

        public static uint Page(bool isLight) => isLight ? Light.Page : Dark.Page;
        public static uint BgPrimary(bool isLight) => Page(isLight);
        public static uint Card(bool isLight) => isLight ? Light.Card : Dark.Card;
        public static uint Stroke(bool isLight) => isLight ? Light.Stroke : Dark.Stroke;
        public static uint BorderDefault(bool isLight) => Stroke(isLight);
        public static uint BorderStrong(bool isLight) => isLight ? Light.BorderStrong : Dark.BorderStrong;
        public static uint Text(bool isLight) => isLight ? Light.Text : Dark.Text;
        public static uint Muted(bool isLight) => isLight ? Light.Muted : Dark.Muted;
        public static uint FgDisabled(bool isLight) => isLight ? Light.FgDisabled : Dark.FgDisabled;
        public static uint NavPane(bool isLight) => isLight ? Light.NavPane : Dark.NavPane;
        public static uint NavSelected(bool isLight) => isLight ? Light.NavSelected : Dark.NavSelected;
        public static uint Catalog(bool isLight) => isLight ? Light.Catalog : Dark.Catalog;
        public static uint Preview(bool isLight) => isLight ? Light.Preview : Dark.Preview;
        public static uint Search(bool isLight) => isLight ? Light.Search : Dark.Search;
        public static uint BgSecondary(bool isLight) => Search(isLight);
        public static uint DangerSubtle(bool isLight) => isLight ? Light.DangerSubtle : Dark.DangerSubtle;
        public static uint ScrollThumb(bool isLight) => isLight ? Light.ScrollThumb : Dark.ScrollThumb;
        public static uint ScrollThumbHover(bool isLight) => isLight ? Light.ScrollThumbHover : Dark.ScrollThumbHover;
        public static uint ScrollThumbPressed(bool isLight) => isLight ? Light.ScrollThumbPressed : Dark.ScrollThumbPressed;
    }

    public static (byte A, byte R, byte G, byte B) Split(uint argb) =>
        ((byte)(argb >> 24), (byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF));

    private static uint Pack(byte a, byte r, byte g, byte b) =>
        ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
}
