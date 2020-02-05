using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Unigram.Common;
using Telegram.Td.Api;
using Unigram.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Unigram.Services;
using Unigram.Views.Chats;

namespace Unigram.Views.Users
{
    public sealed partial class UserCommonChatsPage : Page, IProfileTab, IHandle<UpdateFile>
    {
        public UserCommonChatsViewModel ViewModel => DataContext as UserCommonChatsViewModel;

        public UserCommonChatsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<UserCommonChatsViewModel>();
        }

        public int Index { get => 5; }
        public string Text { get => Strings.Resources.GroupsInCommon; }

        public ListViewBase GetSelector()
        {
            return List;
        }

        public ScrollViewer GetScrollViewer()
        {
            return List.GetScrollViewer();
        }

        private bool _isLocked;

        private bool _isEmbedded;
        public bool IsEmbedded
        {
            get => _isEmbedded;
            set
            {
                Update(value, _isLocked);
            }
        }

        public void Update(bool embedded, bool locked)
        {
            _isEmbedded = embedded;
            _isLocked = locked;

            Header.Visibility = embedded ? Visibility.Collapsed : Visibility.Visible;
            ListHeader.Height = embedded && !locked ? 12 : embedded ? 12 + 16 : 16;
            List.ItemsPanelCornerRadius = new CornerRadius(embedded && !locked ? 0 : 8, embedded && !locked ? 0 : 8, 8, 8);
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Chat chat)
            {
                ViewModel.NavigationService.NavigateToChat(chat);
            }
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = List.ItemContainerStyle;
            }

            args.ItemContainer.ContentTemplate = List.ItemTemplate;

            args.IsContainerPrepared = true;
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

        public void Handle(UpdateFile update)
        {
            this.BeginOnUIThread(() =>
            {
                var panel = List.ItemsPanelRoot as ItemsStackPanel;
                if (panel == null)
                {
                    return;
                }

                if (panel.FirstCacheIndex < 0)
                {
                    return;
                }

                //for (int i = panel.FirstCacheIndex; i <= panel.LastCacheIndex; i++)
                for (int i = 0; i < ViewModel.Items.Count; i++)
                {
                    var chat = ViewModel.Items[i];
                    if (chat.UpdateFile(update.File))
                    {
                        var container = List.ContainerFromItem(chat) as ListViewItem;
                        if (container == null)
                        {
                            return;
                        }

                        var content = container.ContentTemplateRoot as Grid;

                        var photo = content.Children[0] as ProfilePicture;
                        photo.Source = PlaceholderHelper.GetChat(null, chat, 36);
                    }
                }
            });
        }
    }
}
