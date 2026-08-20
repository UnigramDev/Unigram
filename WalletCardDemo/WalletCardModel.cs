using System.Numerics;
using Windows.UI;

namespace WalletCardDemo
{
    /// <summary>Content shown on the wallet card.</summary>
    public sealed class WalletCardModel
    {
        public string HolderName { get; set; }
        public string BalanceTitle { get; set; }
        public string BalanceValue { get; set; }
        public string UsdtAmount { get; set; }
        public string GramAmount { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }

        public static WalletCardModel Demo => new()
        {
            HolderName = "Alicia Torreaux",
            BalanceTitle = "Balance",
            BalanceValue = "$420.69",
            UsdtAmount = "200",
            GramAmount = "91",
            AddressLine1 = "UQAl 1dVi v82p 5sll NyPX JenP",
            AddressLine2 = "JqRf aHrV Gkhm hFcr IjYi nqYK"
        };
    }

    /// <summary>
    /// Metrics and colors from Figma node 103:3863 ("Card", 370x220, dark theme),
    /// carried over verbatim from the iOS sample so both stay comparable.
    /// </summary>
    public static class CardDesign
    {
        public const float Width = 370;
        public const float Height = 220;
        public const float CornerRadius = 20;

        // Angular gradient stops: #0079FF <-> #169AF9 (period 180deg, see CardFront.hlsl).
        public static readonly Color AccentCyan = Color.FromArgb(0xFF, 0x6D, 0xDC, 0xFF);
        public static readonly Color BalanceCyan = Color.FromArgb(0xFF, 0x87, 0xEF, 0xFF);
        public static readonly Color AddressBlue = Color.FromArgb(0xFF, 0x00, 0x6B, 0xD7);

        // Star centers in design points (Figma "Stars" frame 103:3878, each star 8.1x8.1).
        public static readonly Vector2[] StarCenters =
        {
            new(87.12f, 47.00f),
            new(312.60f, 156.00f),
            new(29.72f, 22.00f),
            new(249.06f, 195.00f),
            new(121.97f, 62.00f),
            new(227.53f, 138.00f),
            new(248.03f, 164.00f),
            new(69.69f, 20.00f),
            new(127.09f, 36.00f),
            new(56.37f, 67.00f),
        };

        /// <summary>Per-star twinkle phase (0..1), stored in the stars texture green channel.</summary>
        public static readonly float[] StarPhases =
        {
            0.05f, 0.63f, 0.22f, 0.87f, 0.42f, 0.12f, 0.74f, 0.33f, 0.55f, 0.94f
        };

        public const float StarOuterRadius = 4.05f;
        public const float StarInnerRadius = 1.215f;

        // Fonts. The iOS sample stands in for Figma's Martian Mono with the system
        // monospaced face; Cascadia Mono is the equivalent stand-in here, and
        // Nunito replaces SF Pro Rounded (both are what WalletPage already uses).
        public const string MonoFont = "Cascadia Mono";
        public const string RoundedFont = "/Assets/Fonts/Nunito.ttf#Nunito";
    }
}
