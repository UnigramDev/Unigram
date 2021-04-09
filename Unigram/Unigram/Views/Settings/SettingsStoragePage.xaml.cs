using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsStoragePage : HostedPage
    {
        public SettingsStorageViewModel ViewModel => DataContext as SettingsStorageViewModel;

        public SettingsStoragePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsStorageViewModel>();

            InitializeKeepMediaTicks();
        }

        private void InitializeKeepMediaTicks()
        {
            int j = 0;
            for (int i = 0; i < 4; i++)
            {
                var label = new TextBlock { Text = ConvertKeepMediaTick(i), TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, Style = App.Current.Resources["InfoCaptionTextBlockStyle"] as Style };
                Grid.SetColumn(label, j);

                KeepMediaTicks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

                if (i < 3)
                {
                    KeepMediaTicks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                KeepMediaTicks.Children.Add(label);
                j += 2;
            }

            Grid.SetColumnSpan(KeepMedia, KeepMediaTicks.ColumnDefinitions.Count);
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
                subtitle.Text = FileSizeConverter.Convert(statistics.Size, true);
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = chat == null ? null : PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 36);
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
                return Strings.Resources.KeepMediaForever;
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

        private int ConvertKeepMedia(int value)
        {
            switch (Math.Max(0, Math.Min(30, value)))
            {
                case 0:
                default:
                    return 3;
                case 3:
                    return 0;
                case 7:
                    return 2;
                case 30:
                    return 3;
            }
        }

        private void ConvertKeepMediaBack(double value)
        {
            switch (value)
            {
                case 0:
                    ViewModel.KeepMedia = 3;
                    break;
                case 1:
                    ViewModel.KeepMedia = 7;
                    break;
                case 2:
                    ViewModel.KeepMedia = 30;
                    break;
                case 3:
                    ViewModel.KeepMedia = 0;
                    break;
            }
        }

        private string ConvertKeepMediaTick(double value)
        {
            var days = 0;
            switch (value)
            {
                case 0:
                    days = 3;
                    break;
                case 1:
                    days = 7;
                    break;
                case 2:
                    days = 30;
                    break;
                case 3:
                    days = 0;
                    break;
            }

            if (days < 1)
            {
                return Strings.Resources.KeepMediaForever;
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


        #endregion
    }
}
