using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAdvancedPage : Page
    {
        public SettingsAdvancedViewModel ViewModel => DataContext as SettingsAdvancedViewModel;

        public SettingsAdvancedPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsAdvancedViewModel>();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var chat = args.Item as Chat;

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = ViewModel.ProtoService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {

            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            if (args.DropResult == Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move)
            {
                ViewModel.SetPinnedChats();
            }
        }
    }
}
