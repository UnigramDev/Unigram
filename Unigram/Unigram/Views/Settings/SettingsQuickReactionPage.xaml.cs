using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsQuickReactionPage : HostedPage
    {
        public SettingsQuickReactionViewModel ViewModel => DataContext as SettingsQuickReactionViewModel;

        public SettingsQuickReactionPage()
        {
            InitializeComponent();
            Title = Strings.Resources.DoubleTapSetting;
        }

        private void OnContainerContentChanged(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var element = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
            var reaction = args.Item as SettingsReactionOption;

            var player = element.FindName("Player") as LottieView;
            if (player != null)
            {
                player.FrameSize = new Size(48, 48);

                var file = reaction.Reaction.CenterAnimation.StickerValue;
                if (file.Local.IsFileExisting())
                {
                    player.Source = UriEx.ToLocal(file.Local.Path);
                }
                else
                {
                    player.Source = null;

                    UpdateManager.Subscribe(player, ViewModel.ProtoService, file, UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        ViewModel.ProtoService.DownloadFile(file.Id, 16);
                    }
                }
            }
        }

        private void UpdateFile(object target, File file)
        {
            if (target is LottieView player && player.IsLoaded)
            {
                player.Source = UriEx.ToLocal(file.Local.Path);
            }
        }

        private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                var player = element.FindName("Player") as LottieView;
                if (player != null)
                {
                    player.Play();
                }
            }
        }
    }
}
