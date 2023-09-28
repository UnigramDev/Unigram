//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Profile
{
    public class ProfileTabPage : Page, INavigablePage
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfileTabPage()
        {
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            if (ViewModel.SelectedItems.Count > 0)
            {
                ViewModel.UnselectMessages();
                args.Handled = true;
            }
        }

        #region Context menu

        private void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var message = element.Tag as MessageWithOwner;

            var selected = ViewModel.SelectedItems;
            if (selected.Count > 0)
            {
                if (selected.Contains(message))
                {
                    flyout.CreateFlyoutItem(ViewModel.ForwardSelectedMessages, Strings.ForwardSelected, Icons.Share);

                    //if (chat.CanBeReported)
                    //{
                    //    flyout.CreateFlyoutItem(ViewModel.MessagesReportCommand, "Report Selected", Icons.ShieldError);
                    //}

                    flyout.CreateFlyoutItem(ViewModel.DeleteSelectedMessages, Strings.DeleteSelected, Icons.Delete, destructive: true);
                    flyout.CreateFlyoutItem(ViewModel.UnselectMessages, Strings.ClearSelection);
                    //flyout.CreateFlyoutSeparator();
                    //flyout.CreateFlyoutItem(ViewModel.MessagesCopyCommand, "Copy Selected as Text", Icons.DocumentCopy);
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.SelectMessage, message, Strings.Select, Icons.CheckmarkCircle);
                }
            }
            else
            {

                flyout.CreateFlyoutItem(MessageView_Loaded, ViewModel.ViewMessage, message, Strings.ShowInChat, Icons.ChatEmpty);
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.DeleteMessage, message, Strings.Delete, Icons.Delete, destructive: true);
                flyout.CreateFlyoutItem(MessageForward_Loaded, ViewModel.ForwardMessage, message, Strings.Forward, Icons.Share);
                flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.SelectMessage, message, Strings.Select, Icons.CheckmarkCircle);
                flyout.CreateFlyoutItem(MessageSaveMedia_Loaded, ViewModel.SaveMessageMedia, message, Strings.SaveAs, Icons.SaveAs);
                flyout.CreateFlyoutItem(MessageOpenMedia_Loaded, ViewModel.OpenMessageWith, message, Strings.OpenWith, Icons.OpenIn);
                flyout.CreateFlyoutItem(MessageOpenFolder_Loaded, ViewModel.OpenMessageFolder, message, Strings.ShowInFolder, Icons.FolderOpen);
            }

            args.ShowAt(flyout, element);
        }

        private bool MessageView_Loaded(MessageWithOwner message)
        {
            return true;
        }

        private bool MessageSaveMedia_Loaded(MessageWithOwner message)
        {
            if (message.SelfDestructType is not null || !message.CanBeSaved)
            {
                return false;
            }

            var file = message.GetFile();
            if (file != null)
            {
                return file.Local.IsDownloadingCompleted;
            }

            return false;

            return message.Content switch
            {
                MessagePhoto photo => photo.Photo.GetBig()?.Photo.Local.IsDownloadingCompleted ?? false,
                MessageAudio audio => audio.Audio.AudioValue.Local.IsDownloadingCompleted,
                MessageDocument document => document.Document.DocumentValue.Local.IsDownloadingCompleted,
                MessageVideo video => video.Video.VideoValue.Local.IsDownloadingCompleted,
                _ => false
            };
        }

        private bool MessageOpenMedia_Loaded(MessageWithOwner message)
        {
            if (message.SelfDestructType is not null || !message.CanBeSaved)
            {
                return false;
            }

            return message.Content switch
            {
                MessageAudio audio => audio.Audio.AudioValue.Local.IsDownloadingCompleted,
                MessageDocument document => document.Document.DocumentValue.Local.IsDownloadingCompleted,
                MessageVideo video => video.Video.VideoValue.Local.IsDownloadingCompleted,
                _ => false
            };
        }

        private bool MessageOpenFolder_Loaded(MessageWithOwner message)
        {
            if (message.SelfDestructType is not null || !message.CanBeSaved)
            {
                return false;
            }

            return message.Content switch
            {
                MessagePhoto photo => ViewModel.StorageService.CheckAccessToFolder(photo.Photo.GetBig()?.Photo),
                MessageAudio audio => ViewModel.StorageService.CheckAccessToFolder(audio.Audio.AudioValue),
                MessageDocument document => ViewModel.StorageService.CheckAccessToFolder(document.Document.DocumentValue),
                MessageVideo video => ViewModel.StorageService.CheckAccessToFolder(video.Video.VideoValue),
                _ => false
            };
        }

        private bool MessageDelete_Loaded(MessageWithOwner message)
        {
            return message.CanBeDeletedOnlyForSelf || message.CanBeDeletedForAllUsers;
        }

        private bool MessageForward_Loaded(MessageWithOwner message)
        {
            return message.CanBeForwarded;
        }

        private bool MessageSelect_Loaded(MessageWithOwner message)
        {
            return true;
        }

        #endregion

        #region Selection

        protected string ConvertSelection(int count)
        {
            return Locale.Declension(Strings.R.messages, count);
        }

        protected void OnSelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            ShowHideManagePanel(ScrollingHost.SelectionMode == ListViewSelectionMode.Multiple);
        }

        private bool _manageCollapsed = true;

        private void ShowHideManagePanel(bool show)
        {
            if (_manageCollapsed != show)
            {
                return;
            }

            _manageCollapsed = !show;
            ManagePanel.Visibility = Visibility.Visible;

            var manage = ElementCompositionPreview.GetElementVisual(ManagePanel);
            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);
            manage.Opacity = show ? 0 : 1;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                manage.Opacity = show ? 1 : 0;
                ManagePanel.Visibility = show
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            };

            var offset1 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 48, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3(0, 0, 0));

            var opacity1 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);

            manage.StartAnimation("Translation", offset1);
            manage.StartAnimation("Opacity", opacity1);

            batch.End();
        }

        #endregion

        protected virtual void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                if (sender is ListView)
                {
                    args.ItemContainer = new TableAccessibleChatListViewItem(ViewModel.ClientService);
                }
                else
                {
                    args.ItemContainer = new ChatGridViewItem(ViewModel.ClientService);
                }

                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Message_ContextRequested;
            }

            if (sender.ItemTemplateSelector != null)
            {
                args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            }

            args.IsContainerPrepared = true;
        }



        private ListViewBase _scrollingHost;
        public ListViewBase ScrollingHost => _scrollingHost ??= FindName(nameof(ScrollingHost)) as ListViewBase;

        private Border _headerPlaceholder;
        public Border HeaderPlaceholder => _headerPlaceholder ??= FindName(nameof(HeaderPlaceholder)) as Border;

        private Grid _managePanel;
        public Grid ManagePanel => _managePanel ??= FindName(nameof(ManagePanel)) as Grid;

        public virtual float TopPadding => 48;
    }
}
