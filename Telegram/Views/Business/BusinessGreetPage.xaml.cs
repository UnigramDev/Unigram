using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessGreetPage : HostedPage
    {
        public BusinessGreetViewModel ViewModel => DataContext as BusinessGreetViewModel;

        public BusinessGreetPage()
        {
            InitializeComponent();
            Title = Strings.BusinessGreet;

            SliderHelper.InitializeTicks(Period, PeriodTicks, 4, ConvertPeriod);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "REPLIES_MISSING")
            {
                VisualUtilities.ShakeView(CreateRoot);
            }
        }

        #region Binding

        private Visibility ConvertReplies(QuickReplyShortcut replies)
        {
            if (replies != null)
            {
                Replies.UpdateContent(ViewModel.ClientService, replies, true);
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private string ConvertPeriod(int value)
        {
            switch (value)
            {
                case 0:
                    return Locale.Declension("Days", 7);
                case 1:
                    return Locale.Declension("Days", 14);
                case 2:
                    return Locale.Declension("Days", 21);
                case 3:
                default:
                    return Locale.Declension("Days", 28);
            }
        }

        #endregion

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var content = args.Element as ProfileCell;
            var element = content.DataContext as ChatFolderElement;

            content.UpdateChatFolder(ViewModel.ClientService, element);
        }

        private void Include_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFolderElement;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(viewModel.RemoveIncluded, chat, Strings.StickersRemove, Icons.Delete);
            flyout.ShowAt(sender, args);
        }

        private void Exclude_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var chat = element.DataContext as ChatFolderElement;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(viewModel.RemoveExcluded, chat, Strings.StickersRemove, Icons.Delete);
            flyout.ShowAt(sender, args);
        }
    }
}
