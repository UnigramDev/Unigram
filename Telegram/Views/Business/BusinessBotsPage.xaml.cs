using Microsoft.UI.Xaml.Controls;
using System;
using System.Globalization;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Telegram.ViewModels.Folders;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessBotsPage : HostedPage
    {
        public BusinessBotsViewModel ViewModel => DataContext as BusinessBotsViewModel;

        public BusinessBotsPage()
        {
            InitializeComponent();
            Title = Strings.BusinessBots;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
            OnPropertyChanged(null, new System.ComponentModel.PropertyChangedEventArgs(nameof(ViewModel.BotUserId)));
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.BotUserId))
            {
                if (ViewModel.ClientService.TryGetUser(ViewModel.BotUserId, out User user))
                {
                    UsernameField.Visibility = Visibility.Collapsed;
                    UserBotField.Visibility = Visibility.Visible;
                    UserBotCell.UpdateUser(ViewModel.ClientService, user, 36);
                }
                else
                {
                    UsernameField.Visibility = Visibility.Visible;
                    UserBotField.Visibility = Visibility.Collapsed;
                }
            }
        }

        #region Binding

        private ImageSource ConvertLocation(bool valid, Location location)
        {
            if (valid)
            {
                var latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
                var longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);

                return new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, 15, "320,200")));
            }

            return null;
        }

        private Visibility ConvertClear(string address, bool valid)
        {
            return string.IsNullOrEmpty(address) && !valid
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content && args.Item is User user)
            {
                var cell = content.Children[0] as ProfileCell;
                var button = content.Children[1] as Button;

                cell.UpdateUser(ViewModel.ClientService, user, 36);
                button.Tag = user;
            }
        }

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

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is User user)
            {
                if (user.Type is UserTypeBot { CanConnectToBusiness: true })
                {
                    ViewModel.BotUserId = user.Id;
                    ViewModel.Results.UpdateQuery(string.Empty);
                }
                else
                {
                    ViewModel.ShowPopup(Strings.BusinessBotNotSupportedMessage, Strings.BusinessBotNotSupportedTitle, Strings.OK);
                }
            }
        }
    }
}
