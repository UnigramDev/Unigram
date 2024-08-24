using System;
using System.Globalization;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells.Business;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Telegram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessChatLinksPage : HostedPage, IBusinessChatLinksDelegate
    {
        public BusinessChatLinksViewModel ViewModel => DataContext as BusinessChatLinksViewModel;

        public BusinessChatLinksPage()
        {
            InitializeComponent();
            Title = Strings.BusinessLinks;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
            {
                var restrictPhoneNumber = await ViewModel.ClientService.HasPrivacySettingsRuleAsync<UserPrivacySettingRuleRestrictAll>(new UserPrivacySettingShowPhoneNumber());
                var hasPhoneNumber = !restrictPhoneNumber;
                var hasUsername = user.HasActiveUsername(out string username);

                string text;
                if (hasPhoneNumber && hasUsername)
                {
                    text = string.Format(Strings.BusinessLinksFooterWithUsername, "t.me/" + username, "t.me/+" + user.PhoneNumber);
                }
                else if (hasPhoneNumber)
                {
                    text = string.Format(Strings.BusinessLinksFooterNoUsername, "t.me/+" + user.PhoneNumber);
                }
                else if (hasUsername)
                {
                    text = string.Format(Strings.BusinessLinksFooterNoUsername, "t.me/" + username);
                }
                else
                {
                    CreateFooter.Visibility = Visibility.Collapsed;
                    return;
                }

                // TODO: links
                //var markdown = ClientEx.ParseMarkdown(text);

                CreateFooter.Text = text;
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

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is BusinessChatLinkCell cell && args.Item is BusinessChatLink chatLink)
            {
                cell.UpdateContent(ViewModel.ClientService, chatLink);
            }
        }

        private void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var chatLink = ScrollingHost.ItemFromContainer(sender) as BusinessChatLink;
            if (chatLink is null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(ViewModel.Copy, chatLink, Strings.Copy, Icons.DocumentCopy);
            //flyout.CreateFlyoutItem(ViewModel.Share, chatLink, Strings.ShareFile, Icons.Share);
            flyout.CreateFlyoutItem(ViewModel.Rename, chatLink, Strings.Rename, Icons.Edit);
            flyout.CreateFlyoutItem(ViewModel.Delete, chatLink, Strings.Delete, Icons.Delete, destructive: true);

            flyout.ShowAt(sender, args);
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Open(e.ClickedItem as BusinessChatLink);
        }

        private void CreateFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.CopyLink(XamlRoot, "https://" + e.Url);
        }

        public void UpdateBusinessChatLink(BusinessChatLink chatLink)
        {
            var container = ScrollingHost.ContainerFromItem(chatLink) as SelectorItem;
            var content = container?.ContentTemplateRoot as BusinessChatLinkCell;

            content?.UpdateContent(ViewModel.ClientService, chatLink);
        }
    }
}
