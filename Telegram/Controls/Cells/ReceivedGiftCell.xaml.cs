//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

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

            if (gift.IsSaved)
            {
                if (Hidden != null)
                {
                    Hidden.Visibility = Visibility.Collapsed;

                    Animated.DominantColor = null;
                    Hidden.Background = null;
                }
            }
            else
            {
                FindName(nameof(Hidden));
                Hidden.Visibility = Visibility.Visible;

                if (Animated.DominantColor == null)
                {
                    var brush = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
                    Animated.DominantColor = brush;
                    Hidden.Background = brush;
                }
            }

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
                        Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.AuthorHiddenFilled, 5);
                    }
                    else if (clientService.TryGetUser(gift.SenderId, out User user))
                    {
                        Photo.Source = ProfilePictureSource.User(clientService, user);
                    }
                    else if (clientService.TryGetChat(gift.SenderId, out Chat chat))
                    {
                        Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                    }
                }

                Pattern.Visibility = Visibility.Collapsed;

                Animated.Source = new DelayedFileSource(clientService, regular.Gift.Sticker);

                StarCount.Text = Icons.PremiumStar + " " + (gift.SellStarCount > 0 ? gift.SellStarCount : regular.Gift.StarCount).ToString("N0");

                if (regular.Gift.OverallLimits != null)
                {
                    Ribbon.Visibility = Visibility.Visible;
                    Ribbon.Text = string.Format(Strings.Gift2Limited1OfRibbon, Formatter.ShortNumber(regular.Gift.OverallLimits.TotalCount, true));

                    RibbonTop.Color = _ribbonLimitedTop;
                    RibbonBottom.Color = _ribbonLimitedBottom;

                    if (Ribbon.Background is not LinearGradientBrush)
                    {
                        Ribbon.Background = RibbonGradient;
                    }
                }
                else
                {
                    Ribbon.Visibility = Visibility.Collapsed;
                }

                ResaleStarCountRoot?.Visibility = Visibility.Collapsed;
            }
            else if (gift.Gift is SentGiftUpgraded upgraded)
            {
                var centerColor = upgraded.Gift.Backdrop.Colors.CenterColor.ToColor();
                var edgeColor = upgraded.Gift.Backdrop.Colors.EdgeColor.ToColor();

                Pattern.Update(clientService, upgraded.Gift);

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

                Ribbon.Visibility = Visibility.Visible;

                if (upgraded.Gift.ResaleParameters != null)
                {
                    Ribbon.Text = Strings.Gift2OnSale;

                    RibbonTop.Color = _ribbonResaleTop;
                    RibbonBottom.Color = _ribbonResaleBottom;

                    if (ResaleStarCountRoot != null)
                    {
                        ResaleStarCountRoot.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        FindName(nameof(ResaleStarCountRoot));
                        Grid.SetRow(ResaleStarCountRoot, 0);
                    }

                    ResaleStarCountRoot.Background = new SolidColorBrush(edgeColor.Darken());
                    UpdateResalePrice(upgraded.Gift.ResaleParameters);
                }
                else
                {
                    Ribbon.Text = string.Format(Strings.Gift2Limited1OfRibbon, Formatter.ShortNumber(upgraded.Gift.MaxUpgradedCount, true));

                    RibbonTop.Color = centerColor.Darken();
                    RibbonBottom.Color = edgeColor.Darken();

                    ResaleStarCountRoot?.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdateResalePrice(GiftResaleParameters parameters)
        {
            // The glyphs are drawn for Segoe UI's baseline, so they go inline rather than beside the text.
            ResaleStarCount.Text = parameters.GramOnly
                ? Icons.Gram14 + " " + (parameters.GramCentCount / 100d).ToString("0.##")
                : Icons.Star14 + " " + parameters.StarCount.ToString("N0");
        }

        public void UpdateGift(IClientService clientService, SentGiftUpgraded upgraded)
        {
            StarCountRoot.Visibility = Visibility.Collapsed;

            if (Hidden != null)
            {
                Hidden.Visibility = Visibility.Collapsed;

                Animated.DominantColor = null;
                Hidden.Background = null;
            }

            var centerColor = upgraded.Gift.Backdrop.Colors.CenterColor.ToColor();
            var edgeColor = upgraded.Gift.Backdrop.Colors.EdgeColor.ToColor();

            Pattern.Update(clientService, upgraded.Gift);

            Pinned.Visibility = Visibility.Collapsed;

            Photo.Visibility = Visibility.Collapsed;
            Pattern.Visibility = Visibility.Visible;

            Animated.Source = new DelayedFileSource(clientService, upgraded.Gift.Model.Sticker);

            Ribbon.Visibility = Visibility.Collapsed;

            ResaleStarCountRoot?.Visibility = Visibility.Collapsed;
        }


        public void UpdateGift(IClientService clientService, EmojiStatusTypeUpgradedGift upgraded)
        {
            StarCountRoot.Visibility = Visibility.Collapsed;

            if (Hidden != null)
            {
                Hidden.Visibility = Visibility.Collapsed;

                Animated.DominantColor = null;
                Hidden.Background = null;
            }

            var source = new CustomEmojiFileSource(clientService, upgraded.SymbolCustomEmojiId);
            var centerColor = upgraded.BackdropColors.CenterColor.ToColor();
            var edgeColor = upgraded.BackdropColors.EdgeColor.ToColor();
            var symbolColor = upgraded.BackdropColors.SymbolColor.ToColor();

            Pattern.Update(source, centerColor, edgeColor, symbolColor);

            Pinned.Visibility = Visibility.Collapsed;

            Photo.Visibility = Visibility.Collapsed;
            Pattern.Visibility = Visibility.Visible;

            Animated.Source = new CustomEmojiFileSource(clientService, upgraded.ModelCustomEmojiId);

            Ribbon.Visibility = Visibility.Collapsed;

            ResaleStarCountRoot?.Visibility = Visibility.Collapsed;
        }

        public void UpdateGift(IClientService clientService, GiftForResale gift)
        {
            StarCountRoot.Visibility = Visibility.Collapsed;

            Pattern.Update(clientService, gift.Gift);

            Pinned.Visibility = Visibility.Collapsed;

            Photo.Visibility = Visibility.Collapsed;
            Pattern.Visibility = Visibility.Visible;

            Animated.Source = new DelayedFileSource(clientService, gift.Gift.Model.Sticker);

            var centerColor = gift.Gift.Backdrop.Colors.CenterColor.ToColor();
            var edgeColor = gift.Gift.Backdrop.Colors.EdgeColor.ToColor();

            FindName(nameof(ResaleStarCountRoot));
            ResaleStarCountRoot.Background = new SolidColorBrush(edgeColor.Darken());
            UpdateResalePrice(gift.Gift.ResaleParameters);

            Ribbon.Visibility = Visibility.Visible;
            Ribbon.Text = string.Format("#{0:N0}", gift.Gift.Number);

            RibbonTop.Color = centerColor.Darken();
            RibbonBottom.Color = edgeColor.Darken();
        }

        public void UpdateGift(IClientService clientService, ReceivedGift gift, bool transfer)
        {
            if (gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            StarCountRoot.Visibility = Visibility.Collapsed;

            Pattern.Update(clientService, upgraded.Gift);

            Pinned.Visibility = Visibility.Collapsed;

            // The picker shows this beside available gifts, whose marks a container may still carry.
            PremiumRoot?.Visibility = Visibility.Collapsed;
            Locked?.Visibility = Visibility.Collapsed;

            Photo.Visibility = Visibility.Collapsed;
            Pattern.Visibility = Visibility.Visible;

            Animated.Source = new DelayedFileSource(clientService, upgraded.Gift.Model.Sticker);

            var centerColor = upgraded.Gift.Backdrop.Colors.CenterColor.ToColor();
            var edgeColor = upgraded.Gift.Backdrop.Colors.EdgeColor.ToColor();

            FindName(nameof(ResaleStarCountRoot));
            ResaleStarCountRoot.Background = new SolidColorBrush(edgeColor.Darken());
            ResaleStarCount.Text = Strings.Gift2TransferMine;

            Ribbon.Visibility = Visibility.Visible;
            Ribbon.Text = string.Format("#{0:N0}", upgraded.Gift.Number);

            RibbonTop.Color = centerColor.Darken();
            RibbonBottom.Color = edgeColor.Darken();
        }

        private readonly Color _ribbonResaleTop = Color.FromArgb(0xFF, 0xAC, 0xDC, 0x89);
        private readonly Color _ribbonResaleBottom = Color.FromArgb(0xFF, 0x75, 0xC8, 0x73);

        private readonly Color _ribbonLimitedTop = Color.FromArgb(0xFF, 0x8A, 0xD3, 0xF9);
        private readonly Color _ribbonLimitedBottom = Color.FromArgb(0xFF, 0x51, 0x9D, 0xEA);

        private readonly Color _ribbonSoldOutTop = Color.FromArgb(0xFF, 0xFF, 0x5B, 0x54);
        private readonly Color _ribbonSoldOutBottom = Color.FromArgb(0xFF, 0xED, 0x1D, 0x27);

        private readonly Color _ribbonPremiumTop = Color.FromArgb(0xFF, 0xD7, 0x90, 0x23);
        private readonly Color _ribbonPremiumBottom = Color.FromArgb(0xFF, 0xBF, 0x7D, 0x16);

        /// <param name="resale">The cell that opens the gift's resale listings, beside or instead of the one that buys it.</param>
        /// <param name="upgrade">The price includes the upgrade: the receiver takes this gift only unique.</param>
        public void UpdateGift(IClientService clientService, AvailableGift gift, bool resale, bool upgrade)
        {
            Photo.Visibility = Visibility.Collapsed;
            Pinned.Visibility = Visibility.Collapsed;

            StarCountParticles.Source = new ParticlesImageSource(Color.FromArgb(0x80, 0xE8, 0xAB, 0x02), Native.ParticlesType.Premium);
            Animated.Source = new DelayedFileSource(clientService, gift.Gift.Sticker);

            var listed = resale && gift.ResaleCount > 0;
            var soldOut = gift.Gift.OverallLimits?.RemainingCount == 0;
            var auction = gift.Gift.AuctionInfo;

            // Gold marks what isn't for everyone right now: Premium gifts and running auctions.
            if (!listed && (gift.Gift.IsPremium || auction != null && !soldOut))
            {
                FindName(nameof(PremiumRoot));
                PremiumRoot.Visibility = Visibility.Visible;
            }
            else
            {
                PremiumRoot?.Visibility = Visibility.Collapsed;
            }

            if (!listed && gift.Gift.NextSendDate > clientService.UnixTime)
            {
                FindName(nameof(Locked));
                Locked.Visibility = Visibility.Visible;
            }
            else
            {
                Locked?.Visibility = Visibility.Collapsed;
            }

            if (listed)
            {
                // "+" says there are dearer listings too, unless the cheapest is already at the cap.
                StarCount.Text = gift.ResaleCount > 1 && gift.MinResaleStarCount < clientService.Options.GiftResaleStarCountMax
                    ? Icons.PremiumStar + " " + gift.MinResaleStarCount.ToString("N0") + "+"
                    : Icons.PremiumStar + " " + gift.MinResaleStarCount.ToString("N0");
            }
            else if (auction != null && gift.ResaleCount == 0)
            {
                StarCount.Text = soldOut ? Strings.Gift2AuctionPriceView : Strings.Gift2AuctionPriceJoin;
            }
            else
            {
                var starCount = gift.Gift.StarCount + (upgrade ? gift.Gift.UpgradeStarCount : 0);
                StarCount.Text = Icons.PremiumStar + " " + starCount.ToString("N0");
            }

            if (listed)
            {
                UpdateRibbon(Strings.Gift2Resale, _ribbonResaleTop, _ribbonResaleBottom);
            }
            else if (soldOut)
            {
                UpdateRibbon(Strings.Gift2SoldOut, _ribbonSoldOutTop, _ribbonSoldOutBottom);
            }
            else if (auction != null)
            {
                UpdateRibbon(auction.StartDate > clientService.UnixTime ? Strings.Gift2LimitedAuctionSoon : Strings.Gift2LimitedAuction, _ribbonPremiumTop, _ribbonPremiumBottom);
            }
            else if (gift.Gift.IsPremium)
            {
                UpdateRibbon(Strings.Gift2LimitedPremium, _ribbonPremiumTop, _ribbonPremiumBottom);
            }
            else if (gift.Gift.OverallLimits != null)
            {
                UpdateRibbon(Strings.Gift2LimitedRibbon, _ribbonLimitedTop, _ribbonLimitedBottom);
            }
            else
            {
                Ribbon.Visibility = Visibility.Collapsed;
            }

            Hidden?.Visibility = Visibility.Collapsed;
        }

        private void UpdateRibbon(string text, Color top, Color bottom)
        {
            Ribbon.Visibility = Visibility.Visible;
            Ribbon.Text = text;

            RibbonTop.Color = top;
            RibbonBottom.Color = bottom;
        }
    }
}
