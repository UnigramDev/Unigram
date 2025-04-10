using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI;

namespace Telegram.Controls.Cells
{
    public sealed partial class ReceivedGiftCell : UserControl
    {
        public ReceivedGiftCell()
        {
            InitializeComponent();
        }

        public void UpdateGift(IClientService clientService, ReceivedGift gift)
        {
            StarCountRoot.Visibility = Visibility.Collapsed;

            if (gift.Gift is SentGiftRegular regular)
            {
                if (gift.IsPinned)
                {
                    Photo.Visibility = Visibility.Collapsed;
                    Pinned.Visibility = Visibility.Visible;
                    Pinned.RequestedTheme = ElementTheme.Default;

                    VisualUtilities.DropShadow(Pinned, target: Shadow);
                }
                else
                {
                    Photo.Visibility = Visibility.Visible;
                    Pinned.Visibility = Visibility.Collapsed;

                    if (gift.IsPrivate)
                    {
                        Photo.Source = PlaceholderImage.GetGlyph(Icons.AuthorHiddenFilled, 5);
                    }
                    else if (clientService.TryGetUser(gift.SenderId, out User user))
                    {
                        Photo.SetUser(clientService, user, 24);
                    }
                    else if (clientService.TryGetChat(gift.SenderId, out Chat chat))
                    {
                        Photo.SetChat(clientService, chat, 24);
                    }
                }

                Pattern.Visibility = Visibility.Collapsed;

                Animated.Source = new DelayedFileSource(clientService, regular.Gift.Sticker);

                StarCount.Text = gift.SellStarCount > 0
                    ? gift.SellStarCount.ToString("N0")
                    : regular.Gift.StarCount.ToString("N0");

                if (regular.Gift.TotalCount > 0)
                {
                    RibbonRoot.Visibility = Visibility.Visible;
                    Ribbon.Text = string.Format(Strings.Gift2Limited1OfRibbon, Formatter.ShortNumber(regular.Gift.TotalCount, true));

                    RibbonTop.Color = _ribbonLimitedTop;
                    RibbonBottom.Color = _ribbonLimitedBottom;

                    if (RibbonPath.Fill is not LinearGradientBrush)
                    {
                        RibbonPath.Fill = RibbonGradient;
                    }
                }
                else
                {
                    RibbonRoot.Visibility = Visibility.Collapsed;
                }
            }
            else if (gift.Gift is SentGiftUpgraded upgraded)
            {
                var source = DelayedFileSource.FromSticker(clientService, upgraded.Gift.Symbol.Sticker);
                var centerColor = upgraded.Gift.Backdrop.Colors.CenterColor.ToColor();
                var edgeColor = upgraded.Gift.Backdrop.Colors.EdgeColor.ToColor();

                Pattern.Update(source, centerColor, edgeColor);

                if (gift.IsPinned)
                {
                    Pinned.Visibility = Visibility.Visible;
                    Pinned.RequestedTheme = ElementTheme.Dark;

                    VisualUtilities.DropShadow(Pinned, target: Shadow);
                }
                else
                {
                    Pinned.Visibility = Visibility.Collapsed;
                }

                Photo.Visibility = Visibility.Collapsed;
                Pattern.Visibility = Visibility.Visible;

                Animated.Source = new DelayedFileSource(clientService, upgraded.Gift.Model.Sticker);

                RibbonRoot.Visibility = Visibility.Visible;
                Ribbon.Text = string.Format(Strings.Gift2Limited1OfRibbon, Formatter.ShortNumber(upgraded.Gift.MaxUpgradedCount, true));

                RibbonTop.Color = centerColor.WithBrightness(-0.1f);
                RibbonBottom.Color = edgeColor.WithBrightness(-0.1f);
            }

            if (gift.IsSaved)
            {
                if (Hidden != null)
                {
                    Hidden.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                FindName(nameof(Hidden));
                Hidden.Visibility = Visibility.Visible;
            }
        }

        private readonly Color _ribbonLimitedTop = Color.FromArgb(0xFF, 0x6E, 0xD2, 0xFF);
        private readonly Color _ribbonLimitedBottom = Color.FromArgb(0xFF, 0x35, 0xA5, 0xFC);

        private readonly Color _ribbonSoldOutTop = Color.FromArgb(0xFF, 0xFF, 0x5B, 0x54);
        private readonly Color _ribbonSoldOutBottom = Color.FromArgb(0xFF, 0xED, 0x1D, 0x27);

        public void UpdateGift(IClientService clientService, Gift gift)
        {
            Photo.Visibility = Visibility.Collapsed;
            Pinned.Visibility = Visibility.Collapsed;

            Animated.Source = new DelayedFileSource(clientService, gift.Sticker);

            StarCount.Text = gift.StarCount.ToString("N0");

            if (gift.TotalCount > 0)
            {
                RibbonRoot.Visibility = Visibility.Visible;
                Ribbon.Text = gift.RemainingCount > 0
                    ? Strings.Gift2LimitedRibbon
                    : Strings.Gift2SoldOut;

                RibbonTop.Color = gift.RemainingCount > 0 ? _ribbonLimitedTop : _ribbonSoldOutTop;
                RibbonBottom.Color = gift.RemainingCount > 0 ? _ribbonLimitedBottom : _ribbonSoldOutBottom;
            }
            else
            {
                RibbonRoot.Visibility = Visibility.Collapsed;
            }

            if (Hidden != null)
            {
                Hidden.Visibility = Visibility.Collapsed;
            }
        }
    }
}
