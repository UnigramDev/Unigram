using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels.Settings;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using TdWindows;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Common;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStoragePage : Page
    {
        public SettingsStorageViewModel ViewModel => DataContext as SettingsStorageViewModel;

        public SettingsStoragePage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<SettingsStorageViewModel>();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var statistics = args.Item as StorageStatisticsByChat;

            var chat = ViewModel.ProtoService.GetChat(statistics.ChatId);
            //if (chat == null)
            //{
            //    return;
            //}

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = chat == null ? "Other Chats" : ViewModel.ProtoService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = FileSizeConverter.Convert(statistics.Size);
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = chat == null ? null : PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36, 36);
                photo.Visibility = chat == null ? Visibility.Collapsed : Visibility.Visible;
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;

        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.Clear(e.ClickedItem as StorageStatisticsByChat);
        }

        #region Binding

        private string ConvertTtl(int days)
        {
            if (days < 1)
            {
                return Strings.Android.KeepMediaForever;
            }
            else if (days < 7)
            {
                return Locale.Declension("Days", days);
            }
            else if (days < 30)
            {
                return Locale.Declension("Weeks", 1);
            }

            return Locale.Declension("Months", 1);
        }

        private bool ConvertEnabled(object value)
        {
            return value != null;
        }

        #endregion
    }
}
