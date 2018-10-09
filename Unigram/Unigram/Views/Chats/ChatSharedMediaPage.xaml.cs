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

        public void OnBackRequested(HandledEventArgs args)
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

            var viewModel = new DialogGalleryViewModel(ViewModel.ProtoService, ViewModel.Aggregator, message.ChatId, message);
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

            CreateFlyoutItem(ref flyout, MessageView_Loaded, ViewModel.MessageViewCommand, message, Strings.Resources.ShowInChat);
            CreateFlyoutItem(ref flyout, MessageDelete_Loaded, ViewModel.MessageDeleteCommand, message, Strings.Resources.Delete);
            CreateFlyoutItem(ref flyout, MessageForward_Loaded, ViewModel.MessageForwardCommand, message, Strings.Resources.Forward);
            CreateFlyoutItem(ref flyout, MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Additional.Select);
            CreateFlyoutItem(ref flyout, MessageSave_Loaded, ViewModel.MessageSaveCommand, message, Strings.Additional.SaveAs);

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, Func<Message, Visibility> visibility, ICommand command, object parameter, string text)
        {
            var value = visibility(parameter as Message);
            if (value == Visibility.Visible)
            {
                var flyoutItem = new MenuFlyoutItem();
                //flyoutItem.Loaded += (s, args) => flyoutItem.Visibility = visibility(parameter as TLMessageCommonBase);
                flyoutItem.Command = command;
                flyoutItem.CommandParameter = parameter;
                flyoutItem.Text = text;

                flyout.Items.Add(flyoutItem);
            }
        }

        private Visibility MessageView_Loaded(Message message)
        {
            return Visibility.Visible;
        }

        private Visibility MessageSave_Loaded(Message message)
        {
            return Visibility.Visible;
        }

        private Visibility MessageDelete_Loaded(Message message)
        {
            return message.CanBeDeletedOnlyForSelf || message.CanBeDeletedForAllUsers ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility MessageForward_Loaded(Message message)
        {
            return Visibility.Visible;
        }

        private Visibility MessageSelect_Loaded(Message message)
        {
            return ViewModel.SelectionMode == ListViewSelectionMode.None ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var message = args.Item as Message;
            if (message.Content is MessagePhoto photoMessage)
            {
                if (args.ItemContainer.ContentTemplateRoot is SimpleHyperlinkButton content)
                {
                    var small = photoMessage.Photo.GetSmall();
                    var photo = content.Content as Image;
                    photo.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, small.Photo, 0, 0);
                }
            }
            else if (message.Content is MessageVideo videoMessage && videoMessage.Video.Thumbnail != null)
            {
                if (args.ItemContainer.ContentTemplateRoot is SimpleHyperlinkButton content)
                {
                    var photo = content.Content as Image;
                    photo.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, videoMessage.Video.Thumbnail.Photo, 0, 0);
                }
            }
            else if (message.Content is MessageDocument)
            {
                if (args.ItemContainer.ContentTemplateRoot is SharedFileCell content)
                {
                    content.UpdateMessage(ViewModel.ProtoService, ViewModel, message);
                }
            }
            else if (message.Content is MessageText)
            {
                if (args.ItemContainer.ContentTemplateRoot is SharedLinkCell content)
                {
                    content.UpdateMessage(message);
                }
            }

            var element = args.ItemContainer.ContentTemplateRoot as FrameworkElement;
            element.Tag = message;
        }

        public void UpdateFile(Telegram.Td.Api.File file)
        {
            foreach (Message message in ScrollingMedia.Items)
            {
                if (message.UpdateFile(file))
                {
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
                            var thumbnail = content.Content as Image;
                            thumbnail.Source = PlaceholderHelper.GetBitmap(ViewModel.ProtoService, thumb.Photo, 0, 0);
                        }
                    }
                }
            }

            foreach (Message message in ScrollingFiles.Items)
            {
                if (message.UpdateFile(file))
                {
                    var container = ScrollingFiles.ContainerFromItem(message) as ListViewItem;
                    if (container == null)
                    {
                        continue;
                    }

                    var document = message.Content as MessageDocument;
                    var content = container.ContentTemplateRoot as SharedFileCell;

                    if (document == null || document.Document == null || document.Document.DocumentValue == null)
                    {
                        continue;
                    }

                    if (file.Id == document.Document.DocumentValue.Id)
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
    }
}
