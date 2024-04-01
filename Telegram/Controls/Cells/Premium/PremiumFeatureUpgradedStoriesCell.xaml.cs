//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells.Premium
{
    public sealed partial class PremiumFeatureUpgradedStoriesCell : UserControl, IPremiumFeatureCell
    {
        public PremiumFeatureUpgradedStoriesCell()
        {
            InitializeComponent();
        }

        public void UpdateFeature(IClientService clientService)
        {
            ScrollingHost.ItemsSource = new object[]
            {
                new PremiumStoryFeaturePriorityOrder(),
                new PremiumStoryFeatureStealthMode(),
                new PremiumStoryFeatureVideoQuality(),
                new PremiumStoryFeaturePermanentViewsHistory(),
                new PremiumStoryFeatureCustomExpirationDuration(),
                new PremiumStoryFeatureSaveStories(),
                new PremiumLimitTypeStoryCaptionLength(),
                new PremiumStoryFeatureLinksAndFormatting()
            };

            if (clientService.TryGetChat(clientService.Options.MyId, out Chat chat) &&
                clientService.TryGetUser(chat, out User user))
            {
                Segments.UpdateSegments(96, 8, 8, 3);
                Photo.SetUser(clientService, user, 96);
            }

            //clientService.Send(new ViewPremiumFeature(new PremiumFeatureUpgradedStories()));
        }

        private readonly Color[] _gradient = new Color[]
        {
            Color.FromArgb(0xFF, 0x00, 0x7A, 0xFF),
            Color.FromArgb(0xFF, 0x79, 0x8A, 0xFF),
            Color.FromArgb(0xFF, 0xAC, 0x64, 0xF3),
            Color.FromArgb(0xFF, 0xC4, 0x56, 0xAE),
            Color.FromArgb(0xFF, 0xE9, 0x5D, 0x44),
            Color.FromArgb(0xFF, 0xF2, 0x82, 0x2A),
            Color.FromArgb(0xFF, 0xE7, 0xAD, 0x19)
        };

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var feature = args.Item;
            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var iconValue = string.Empty;
            var titleValue = string.Empty;
            var subtitleValue = string.Empty;

            switch (feature)
            {
                case PremiumStoryFeaturePriorityOrder:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesPriority;
                    subtitleValue = Strings.PremiumStoriesPriorityDescription;
                    break;
                case PremiumStoryFeatureStealthMode:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesStealth;
                    subtitleValue = Strings.PremiumStoriesStealthDescription;
                    break;
                case PremiumStoryFeatureVideoQuality:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesQuality;
                    subtitleValue = Strings.PremiumStoriesQualityDescription;
                    break;
                case PremiumStoryFeaturePermanentViewsHistory:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesViews;
                    subtitleValue = Strings.PremiumStoriesViewsDescription;
                    break;
                case PremiumStoryFeatureCustomExpirationDuration:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesExpiration;
                    subtitleValue = Strings.PremiumStoriesExpirationDescription;
                    break;
                case PremiumStoryFeatureSaveStories:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesSaveToGallery;
                    subtitleValue = Strings.PremiumStoriesSaveToGalleryDescription;
                    break;
                case PremiumLimitTypeStoryCaptionLength:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesCaption;
                    subtitleValue = Strings.PremiumStoriesCaptionDescription;
                    break;
                case PremiumStoryFeatureLinksAndFormatting:
                    iconValue = Icons.Stories;
                    titleValue = Strings.PremiumStoriesFormatting;
                    subtitleValue = Strings.PremiumStoriesFormattingDescription;
                    break;
            }

            var title = content.FindName("Title") as TextBlock;
            var subtitle = content.FindName("Subtitle") as TextBlock;
            var icon = content.FindName("Icon") as TextBlock;

            var item = (double)args.ItemIndex;
            var total = sender.Items.Count - 1;
            var length = _gradient.Length - 1;

            var index = (int)(item / total * length);

            title.Text = titleValue;
            subtitle.Text = subtitleValue;
            icon.Text = iconValue;
            icon.Foreground = new SolidColorBrush(_gradient[index]);

            args.Handled = true;
        }

        public void PlayAnimation()
        {
            var scrollingHost = ScrollingHost.GetScrollViewer();
            scrollingHost?.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChangedEvent), true);
        }

        public void StopAnimation()
        {
            var scrollingHost = ScrollingHost.GetScrollViewer();
            scrollingHost?.RemoveHandler(PointerWheelChangedEvent, new PointerEventHandler(OnPointerWheelChangedEvent));
        }

        private void OnPointerWheelChangedEvent(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse && sender is ScrollViewer scrollingHost)
            {
                var currentPoint = e.GetCurrentPoint(this);
                if (currentPoint.Properties.MouseWheelDelta > 0 && scrollingHost.VerticalOffset == 0)
                {
                    var parent = this.GetParent<FlipView>();
                    if (parent?.SelectedIndex > 0 && parent.SelectedItem is PremiumFeatureUpgradedStories or BusinessFeatureUpgradedStories)
                    {
                        parent.SelectedIndex--;
                        StopAnimation();
                    }
                }
            }
        }
    }
}
