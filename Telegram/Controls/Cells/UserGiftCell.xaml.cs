using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Cells
{
    public sealed partial class UserGiftCell : UserControl
    {
        public UserGiftCell()
        {
            InitializeComponent();
        }

        public void UpdateUserGift(IClientService clientService, UserGift gift)
        {
            if (gift.IsPrivate)
            {
                Photo.Source = PlaceholderImage.GetGlyph(Icons.AuthorHiddenFilled, 5);
            }
            else if (clientService.TryGetUser(gift.SenderUserId, out User user))
            {
                Photo.SetUser(clientService, user, 24);
            }

            Animated.Source = new DelayedFileSource(clientService, gift.Gift.Sticker);

            StarCount.Text = gift.SellStarCount > 0
                ? gift.SellStarCount.ToString("N0")
                : gift.Gift.StarCount.ToString("N0");

            if (gift.Gift.TotalCount > 0)
            {
                RibbonRoot.Visibility = Visibility.Visible;
                Ribbon.Text = string.Format(Strings.Gift2Limited1OfRibbon, Formatter.ShortNumber(gift.Gift.TotalCount));
            }
            else
            {
                RibbonRoot.Visibility = Visibility.Collapsed;
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

        public void UpdateGift(IClientService clientService, Gift gift)
        {
            Photo.Visibility = Visibility.Collapsed;

            Animated.Source = new DelayedFileSource(clientService, gift.Sticker);

            StarCount.Text = gift.StarCount.ToString("N0");

            if (gift.TotalCount > 0)
            {
                RibbonRoot.Visibility = Visibility.Visible;
                Ribbon.Text = Strings.Gift2LimitedRibbon;
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
