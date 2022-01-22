using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupReactionsPage : HostedPage, IChatDelegate
    {
        public SupergroupReactionsViewModel ViewModel => DataContext as SupergroupReactionsViewModel;

        public SupergroupReactionsPage()
        {
            InitializeComponent();
        }

        private void OnContainerContentChanged(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var element = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
            var reaction = args.Item as SupergroupReactionOption;

            var player = element.FindName("Player") as LottieView;
            if (player != null)
            {
                player.FrameSize = new Windows.Graphics.SizeInt32 { Width = 48, Height = 48 };

                var file = reaction.Reaction.CenterAnimation.StickerValue;
                if (file.Local.IsDownloadingCompleted)
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

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            Enable.Footer = chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel ? Strings.Resources.EnableReactionsChannelInfo : Strings.Resources.EnableReactionsGroupInfo;
        }

        public void UpdateChatTitle(Chat chat) { }

        public void UpdateChatPhoto(Chat chat) { }

        #endregion

    }
}
