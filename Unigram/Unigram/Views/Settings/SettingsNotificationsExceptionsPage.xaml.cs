using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Settings;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsNotificationsExceptionsPage : HostedPage
    {
        public SettingsNotificationsExceptionsViewModel ViewModel => DataContext as SettingsNotificationsExceptionsViewModel;

        public SettingsNotificationsExceptionsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsNotificationsExceptionsViewModel>();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Chat chat)
            {
                ViewModel.NavigationService.NavigateToChat(chat);
            }
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
                //var label = content.Children[2] as TextBlock;
                //var exception = chat.NotificationSettings;

                //String text;
                //bool enabled;
                //bool custom = exception.hasCustom;
                //int value = exception.notify;
                //int delta = exception.MuteFor;
                //if (value == 3 && delta != int.MaxValue)
                //{
                //    delta -= DateTime.Now.ToTimestamp();
                //    if (delta <= 0)
                //    {
                //        if (custom)
                //        {
                //            text = Strings.Resources.NotificationsCustom;
                //        }
                //        else
                //        {
                //            text = Strings.Resources.NotificationsUnmuted;
                //        }
                //    }
                //    else if (delta < 60 * 60)
                //    {
                //        text = string.Format(Strings.Resources.WillUnmuteIn, Locale.Declension("Minutes", delta / 60));
                //    }
                //    else if (delta < 60 * 60 * 24)
                //    {
                //        text = string.Format(Strings.Resources.WillUnmuteIn, Locale.Declension("Hours", (int)Math.Ceiling(delta / 60.0f / 60)));
                //    }
                //    else if (delta < 60 * 60 * 24 * 365)
                //    {
                //        text = string.Format(Strings.Resources.WillUnmuteIn, Locale.Declension("Days", (int)Math.Ceiling(delta / 60.0f / 60 / 24)));
                //    }
                //    else
                //    {
                //        text = null;
                //    }
                //}
                //else
                //{
                //    if (value == 0)
                //    {
                //        enabled = true;
                //    }
                //    else if (value == 1)
                //    {
                //        enabled = true;
                //    }
                //    else if (value == 2)
                //    {
                //        enabled = false;
                //    }
                //    else
                //    {
                //        enabled = false;
                //    }
                //    if (enabled && custom)
                //    {
                //        text = Strings.Resources.NotificationsCustom;
                //    }
                //    else
                //    {
                //        text = enabled ? Strings.Resources.NotificationsUnmuted : Strings.Resources.NotificationsMuted;
                //    }
                //}
                //if (text == null)
                //{
                //    text = Strings.Resources.NotificationsOff;
                //}

                //label.Text = text;
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
