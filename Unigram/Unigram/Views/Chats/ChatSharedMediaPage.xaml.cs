using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using LinqToVisualTree;
using System.Threading.Tasks;
using Unigram.Controls;
using Template10.Common;
using System.ComponentModel;
using Unigram.Common;
using Windows.UI.Core;
using Windows.System;
using System.Windows.Input;
using Unigram.Strings;
using Unigram.ViewModels.Dialogs;
using Telegram.Td.Api;
using Unigram.Controls.Views;
using Unigram.ViewModels.Delegates;
using System.Reactive.Linq;
using Unigram.ViewModels.Chats;
using Unigram.Controls.Cells;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.Controls.Chats;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatSharedMediaPage : Page, INavigablePage, IFileDelegate
    {
        public ChatSharedMediaViewModel ViewModel => DataContext as ChatSharedMediaViewModel;

        public ChatSharedMediaPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ChatSharedMediaViewModel, IFileDelegate>(this);

            ViewModel.PropertyChanged += OnPropertyChanged;

            MediaHeader.Content = Camelize(Strings.Resources.SharedMediaTab);
            FilesHeader.Content = Camelize(Strings.Resources.SharedFilesTab);
            LinksHeader.Content = Camelize(Strings.Resources.SharedLinksTab);
            MusicHeader.Content = Camelize(Strings.Resources.SharedMusicTab);
            VoiceHeader.Content = Camelize(Strings.Resources.SharedVoiceTab);

            InitializeSearch(SearchFiles, () => new SearchMessagesFilterDocument());
            InitializeSearch(SearchLinks, () => new SearchMessagesFilterUrl());
            InitializeSearch(SearchMusic, () => new SearchMessagesFilterAudio());
        }

        private string Camelize(string text)
        {
            return text.Substring(0, 1).ToUpper() + text.Substring(1).ToLower();
        }

        private void InitializeSearch(TextBox field, Func<SearchMessagesFilter> filter)
        {
            var observable = Observable.FromEventPattern<TextChangedEventArgs>(field, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                ViewModel.Find(filter(), field.Text);
            });
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("SelectedItems"))
            {
                switch (ScrollingHost.SelectedIndex)
                {
                    case 0:
                        ScrollingMedia.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 1:
                        ScrollingFiles.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 2:
                        ScrollingLinks.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 3:
                        ScrollingMusic.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                    case 4:
                        ScrollingVoice.SelectedItems.AddRange(ViewModel.SelectedItems);
                        break;
                }
            }
        }

        public void OnBackRequested(HandledRoutedEventArgs args)
        {
            if (ViewModel.SelectionMode != ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
                args.Handled = true;
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.Tag as Message;

            var viewModel = new ChatGalleryViewModel(ViewModel.ProtoService, ViewModel.Aggregator, message.ChatId, message, true);
            await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => element);
        }

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            //ScrollingMedia.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingFiles.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingLinks.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;
            //ScrollingMusic.IsItemClickEnabled = ViewModel.SelectionMode == ListViewSelectionMode.None;

            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ManagePanel.Visibility = Visibility.Collapsed;
                //InfoPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ManagePanel.Visibility = Visibility.Visible;
                //InfoPanel.Visibility = Visibility.Collapsed;
            }

            ViewModel.MessagesForwardCommand.RaiseCanExecuteChanged();
            ViewModel.MessagesDeleteCommand.RaiseCanExecuteChanged();
        }

        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ViewModel.SelectedItems = new List<Message>(((ListViewBase)sender).SelectedItems.Cast<Message>());
            }
        }

        private bool ConvertSelectionMode(ListViewSelectionMode mode)
        {
            List_SelectionModeChanged(null, null);
            return mode == ListViewSelectionMode.None ? false : true;
        }

        #region Context menu

        private void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var message = element.Tag as Message;

            flyout.CreateFlyoutItem(MessageView_Loaded, ViewModel.MessageViewCommand, message, Strings.Resources.ShowInChat, new FontIcon { Glyph = Icons.Message });
            flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.MessageDeleteCommand, message, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
            flyout.CreateFlyoutItem(MessageForward_Loaded, ViewModel.MessageForwardCommand, message, Strings.Resources.Forward, new FontIcon { Glyph = Icons.Forward });
            flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Additional.Select, new FontIcon { Glyph = Icons.Select });
            flyout.CreateFlyoutItem(MessageSave_Loaded, ViewModel.MessageSaveCommand, message, Strings.Additional.SaveAs, new FontIcon { Glyph = Icons.SaveAs });

            args.ShowAt(flyout, element);
        }

        private bool MessageView_Loaded(Message message)
        {
            return true;
        }

        private bool MessageSave_Loaded(Message message)
        {
            return true;
        }

        private bool MessageDelete_Loaded(Message message)
        {
            return message.CanBeDeletedOnlyForSelf || message.CanBeDeletedForAllUsers;
        }

        private bool MessageForward_Loaded(Message message)
        {
            return message.CanBeForwarded;
        }

        private bool MessageSelect_Loaded(Message message)
        {
            return ViewModel.SelectionMode == ListViewSelectionMode.None;
        }

        #endregion

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                if (sender is ListView)
                {
                    args.ItemContainer = new AccessibleChatListViewItem(ViewModel.ProtoService);
                }
                else
                {
                    args.ItemContainer = new ChatGridViewItem(ViewModel.ProtoService);
                }

                args.ItemContainer.Style = sender.ItemContainerStyle;
            }

            args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var message = args.Item as Message;
            if (args.ItemContainer.ContentTemplateRoot is SimpleHyperlinkButton hyperlink)
            {
                if (message.Content is MessagePhoto photoMessage)
                {
                    var small = photoMessage.Photo.GetSmall();
                    var photo = hyperlink.Content as Image;
                    photo.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, 0, 0);
                }
                else if (message.Content is MessageVideo videoMessage && videoMessage.Video.Thumbnail != null)
                {
                    var grid = hyperlink.Content as Grid;
                    var photo = grid.Children[0] as Image;
                    photo.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, videoMessage.Video.Thumbnail.Photo, 0, 0);

                    var panel = grid.Children[1] as Grid;
                    var duration = panel.Children[1] as TextBlock;
                    duration.Text = videoMessage.Video.GetDuration();
                }
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedFileCell fileCell)
            {
                fileCell.UpdateMessage(ViewModel.ProtoService, ViewModel, message);
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedLinkCell linkCell)
            {
                linkCell.UpdateMessage(ViewModel.ProtoService, ViewModel.NavigationService, message);
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedAudioCell audioCell)
            {
                audioCell.UpdateMessage(ViewModel.PlaybackService, ViewModel.ProtoService, message);
            }
            else if (args.ItemContainer.ContentTemplateRoot is SharedVoiceCell voiceCell)
            {
                voiceCell.UpdateMessage(ViewModel.PlaybackService, ViewModel.ProtoService, message);
            }
            else if (message.Content is MessageHeaderDate && args.ItemContainer.ContentTemplateRoot is Border content && content.Child is TextBlock header)
            {
                header.Text = DateTimeToFormatConverter.ConvertMonthGrouping(Utils.UnixTimestampToDateTime(message.Date));
            }

            if (args.ItemContainer.ContentTemplateRoot is FrameworkElement element)
            {
                element.Tag = message;
            }
        }

        public void UpdateFile(Telegram.Td.Api.File file)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            if (viewModel.Media != null && viewModel.Media.TryGetMessagesForFileId(file.Id, out IList<Message> messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = ScrollingMedia.ContainerFromItem(message) as GridViewItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var content = container.ContentTemplateRoot as SimpleHyperlinkButton;
                    if (content == null)
                    {
                        continue;
                    }

                    if (message.Content is MessagePhoto photo)
                    {
                        var small = photo.Photo.GetSmall();
                        if (small != null && small.Photo.Id == file.Id && file.Local.IsDownloadingCompleted)
                        {
                            var thumbnail = content.Content as Image;
                            thumbnail.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, 0, 0);
                        }
                    }
                    else if (message.Content is MessageVideo video)
                    {
                        var thumb = video.Video.Thumbnail;
                        if (thumb != null && thumb.Photo.Id == file.Id && file.Local.IsDownloadingCompleted)
                        {
                            var grid = content.Content as Grid;
                            var thumbnail = grid.Children[0] as Image;
                            thumbnail.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, thumb.Photo, 0, 0);
                        }
                    }
                }

                if (file.Local.IsDownloadingCompleted && file.Remote.IsUploadingCompleted)
                {
                    messages.Clear();
                }
            }

            if (viewModel.Files != null && viewModel.Files.TryGetMessagesForFileId(file.Id, out messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = ScrollingFiles.ContainerFromItem(message) as ListViewItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var document = message.GetFile();
                    var content = container.ContentTemplateRoot as SharedFileCell;

                    if (document == null || content == null)
                    {
                        continue;
                    }

                    if (file.Id == document.Id)
                    {
                        content.UpdateFile(message, file);
                    }
                }
            }

            if (viewModel.Music != null && viewModel.Music.TryGetMessagesForFileId(file.Id, out messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = ScrollingMusic.ContainerFromItem(message) as ListViewItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var document = message.GetFile();
                    var content = container.ContentTemplateRoot as SharedFileCell;

                    if (document == null || content == null)
                    {
                        continue;
                    }

                    if (file.Id == document.Id)
                    {
                        content.UpdateFile(message, file);
                    }
                }
            }

            if (viewModel.Voice != null && viewModel.Voice.TryGetMessagesForFileId(file.Id, out messages))
            {
                foreach (var message in messages)
                {
                    message.UpdateFile(file);

                    var container = ScrollingVoice.ContainerFromItem(message) as ListViewItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var document = message.GetFile();
                    var content = container.ContentTemplateRoot as SharedFileCell;

                    if (document == null || content == null)
                    {
                        continue;
                    }

                    if (file.Id == document.Id)
                    {
                        content.UpdateFile(message, file);
                    }
                }
            }
        }

        private void NavigationView_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer == MediaHeader)
            {
                ScrollingHost.SelectedIndex = 0;
            }
            else if (args.InvokedItemContainer == FilesHeader)
            {
                ScrollingHost.SelectedIndex = 1;
            }
            else if (args.InvokedItemContainer == LinksHeader)
            {
                ScrollingHost.SelectedIndex = 2;
            }
            else if (args.InvokedItemContainer == MusicHeader)
            {
                ScrollingHost.SelectedIndex = 3;
            }
            else if (args.InvokedItemContainer == VoiceHeader)
            {
                ScrollingHost.SelectedIndex = 4;
            }
        }

        private void ScrollingHost_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedIndex == 0)
            {
                Header.SelectedItem = MediaHeader;
            }
            else if (ScrollingHost.SelectedIndex == 1)
            {
                Header.SelectedItem = FilesHeader;
            }
            else if (ScrollingHost.SelectedIndex == 2)
            {
                Header.SelectedItem = LinksHeader;
            }
            else if (ScrollingHost.SelectedIndex == 3)
            {
                Header.SelectedItem = MusicHeader;
            }
            else if (ScrollingHost.SelectedIndex == 4)
            {
                Header.SelectedItem = VoiceHeader;
            }
        }

        private void Header_BackRequested(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs args)
        {
            Frame.GoBack();
        }
    }
}
