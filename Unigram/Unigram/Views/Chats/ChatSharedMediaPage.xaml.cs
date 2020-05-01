﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Controls.Chats;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

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

            InitializeSearch(SearchFiles, () => new SearchMessagesFilterDocument());
            InitializeSearch(SearchLinks, () => new SearchMessagesFilterUrl());
            InitializeSearch(SearchMusic, () => new SearchMessagesFilterAudio());
            InitializeSearch(SearchVoice, () => new SearchMessagesFilterVoiceNote());
        }

        private bool _isLocked = false;

        private bool _isEmbedded;
        public bool IsEmbedded
        {
            get => _isEmbedded;
            set
            {
                Update(value, _isLocked);
            }
        }

        private IProfileTab _tab;
        public IProfileTab Tab
        {
            get => _tab;
            set
            {
                if (_tab != null && _tab is Page old)
                {
                    old.Loaded -= Tab_Loaded;

                    ScrollingHost.Items.RemoveAt(_tab.Index);
                    Header.MenuItems.RemoveAt(_tab.Index);
                }

                _tab = value;

                if (value != null && value is Page page)
                {
                    page.Loaded += Tab_Loaded;

                    var pivotItem = new PivotItem
                    {
                        Header = value.Text,
                        Content = page
                    };

                    ScrollingHost.Items.Insert(value.Index, pivotItem);

                    var header = new Microsoft.UI.Xaml.Controls.NavigationViewItem
                    {
                        Content = value.Text,
                        IsSelected = value.Index == 0
                    };

                    Header.MenuItems.Insert(value.Index, header);
                }
            }
        }

        private void Update(bool embedded, bool locked)
        {
            _tab?.Update(embedded, locked);

            _isEmbedded = embedded;
            _isLocked = locked;

            var previous = (float)Header.ActualWidth;
            var size = embedded && !locked ? 640 : (float)ActualWidth;

            Header.IsBackEnabled = !embedded;
            Header.IsBackButtonVisible = embedded ? Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Collapsed : Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Visible;
            HeaderPanel.CornerRadius = new CornerRadius(embedded && !locked ? 8 : 0, embedded && !locked ? 8 : 0, 0, 0);
            HeaderPanel.MaxWidth = embedded && !locked ? 640 : double.PositiveInfinity;
            HeaderPanel.Margin = new Thickness(embedded && !locked ? 12 : 0, 0, embedded && !locked ? 12 : 0, 0);

            HeaderMedia.Padding = new Thickness(0, embedded && !locked ? 12 : embedded ? 12 + 8 : 8, 0, 0);
            HeaderFiles.Padding = HeaderLinks.Padding = HeaderMusic.Padding = HeaderVoice.Padding = new Thickness(0, embedded && !locked ? 12 : embedded ? 12 + 16 : 16, 0, 8);
            HeaderFiles.Radius = HeaderLinks.Radius = HeaderMusic.Radius = HeaderVoice.Radius = new CornerRadius(embedded && !locked ? 0 : 8, embedded && !locked ? 0 : 8, 8, 8);

            var header = ElementCompositionPreview.GetElementVisual(Header);
            var animator = ElementCompositionPreview.GetElementVisual(HeaderAnimator);

            var offset = header.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(0, new Vector3(previous > size ? -((previous - size) / 2) : (size - previous) / 2, 0, 0));
            offset.InsertKeyFrame(1, new Vector3(0));

            var scale = header.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, new Vector3(previous / size, 1, 1));
            scale.InsertKeyFrame(1, new Vector3(1));

            header.StartAnimation("Offset", offset);
            animator.StartAnimation("Offset", offset);
            animator.StartAnimation("Scale", scale);
        }

        public ScrollViewer GetScrollViewer()
        {
            var tab = _tab;
            var shift = 0;

            if (tab?.Index < 1)
            {
                shift -= 1;
            }

            switch (ScrollingHost.SelectedIndex + shift)
            {
                case 0:
                    return ScrollingMedia.GetScrollViewer();
                case 1:
                    return ScrollingFiles.GetScrollViewer();
                case 2:
                    return ScrollingLinks.GetScrollViewer();
                case 3:
                    return ScrollingMusic.GetScrollViewer();
                case 4:
                    return ScrollingVoice.GetScrollViewer();
            }

            if (ScrollingHost.SelectedIndex == tab.Index)
            {
                return tab.GetScrollViewer();
            }

            return null;
        }

        public void SetScrollMode(bool enable)
        {
            foreach (var scrollViewer in GetScrollViewers())
            {
                if (enable)
                {
                    scrollViewer.VerticalScrollMode = ScrollMode.Auto;
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    scrollViewer.ChangeView(null, 12, null, true);
                }
                else
                {
                    scrollViewer.ChangeView(null, 12, null, true);
                    scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                }

                Update(_isEmbedded, enable);
            }
        }

        private IEnumerable<ScrollViewer> GetScrollViewers()
        {
            var viewer1 = ScrollingMedia.GetScrollViewer();
            if (viewer1 != null)
            {
                yield return viewer1;
            }

            var viewer2 = ScrollingFiles.GetScrollViewer();
            if (viewer2 != null)
            {
                yield return viewer2;
            }

            var viewer3 = ScrollingLinks.GetScrollViewer();
            if (viewer3 != null)
            {
                yield return viewer3;
            }

            var viewer4 = ScrollingMusic.GetScrollViewer();
            if (viewer4 != null)
            {
                yield return viewer4;
            }

            var viewer5 = ScrollingVoice.GetScrollViewer();
            if (viewer5 != null)
            {
                yield return viewer5;
            }

            var viewer6 = _tab?.GetScrollViewer();
            if (viewer6 != null)
            {
                yield return viewer6;
            }
        }

        public event EventHandler<ScrollViewerViewChangedEventArgs> ViewChanged;
        public event EventHandler<EventArgs> ViewRequested;

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
                var tab = _tab;
                var shift = 0;

                if (tab?.Index < 1)
                {
                    shift -= 1;
                }

                switch (ScrollingHost.SelectedIndex + shift)
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
            var tab = _tab;
            var shift = 0;

            if (tab?.Index < 1)
            {
                shift += 1;
            }

            if (args.InvokedItemContainer == MediaHeader)
            {
                ScrollingHost.SelectedIndex = 0 + shift;
            }
            else if (args.InvokedItemContainer == FilesHeader)
            {
                ScrollingHost.SelectedIndex = 1 + shift;
            }
            else if (args.InvokedItemContainer == LinksHeader)
            {
                ScrollingHost.SelectedIndex = 2 + shift;
            }
            else if (args.InvokedItemContainer == MusicHeader)
            {
                ScrollingHost.SelectedIndex = 3 + shift;
            }
            else if (args.InvokedItemContainer == VoiceHeader)
            {
                ScrollingHost.SelectedIndex = 4 + shift;
            }
            else if (args.InvokedItemContainer == Header.MenuItems[tab.Index])
            {
                ScrollingHost.SelectedIndex = tab.Index;
            }
        }

        private void ScrollingHost_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tab = _tab;
            var shift = 0;

            if (tab?.Index < 1)
            {
                shift -= 1;
            }

            switch (ScrollingHost.SelectedIndex + shift)
            {
                case 0:
                    Header.SelectedItem = MediaHeader;
                    break;
                case 1:
                    Header.SelectedItem = FilesHeader;
                    break;
                case 2:
                    Header.SelectedItem = LinksHeader;
                    break;
                case 3:
                    Header.SelectedItem = MusicHeader;
                    break;
                case 4:
                    Header.SelectedItem = VoiceHeader;
                    break;
            }
        }

        private void Header_BackRequested(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs args)
        {
            //Frame.GoBack();
        }

        private void Scrolling_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isEmbedded)
            {
                return;
            }

            var selector = sender as ListViewBase;
            selector.ItemsPanelRoot.MinHeight = ScrollingHost.ActualHeight + 12;

            if (selector == ScrollingMedia)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingMedia_SizeChanged;
            }
            else if (selector == ScrollingFiles)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingFiles_SizeChanged;
            }
            else if (selector == ScrollingLinks)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingLinks_SizeChanged;
            }
            else if (selector == ScrollingMusic)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingMusic_SizeChanged;
            }
            else if (selector == ScrollingVoice)
            {
                selector.ItemsPanelRoot.SizeChanged += ScrollingVoice_SizeChanged;
            }

            var scrollViewer = selector.GetScrollViewer();
            scrollViewer.ChangeView(null, 12, null, true);
            scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        }

        private void Tab_Loaded(object sender, RoutedEventArgs e)
        {
            var tab = sender as IProfileTab;
            tab.Update(_isEmbedded, _isLocked);

            var selector = tab.GetSelector();
            selector.ItemsPanelRoot.MinHeight = ScrollingHost.ActualHeight + 12;

            selector.ItemsPanelRoot.SizeChanged += Tab_SizeChanged;

            var scrollViewer = selector.GetScrollViewer();
            scrollViewer.ChangeView(null, 12, null, true);
            scrollViewer.VerticalScrollMode = ScrollMode.Disabled;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ViewChanged?.Invoke(sender, e);
        }

        private void ScrollingHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isEmbedded)
            {
                return;
            }

            if (ScrollingMedia.ItemsPanelRoot != null)
            {
                ScrollingMedia.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingMedia.GetScrollViewer().ChangeView(null, 12, null, true);
            }

            if (ScrollingFiles.ItemsPanelRoot != null)
            {
                ScrollingFiles.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingFiles.GetScrollViewer().ChangeView(null, 12, null, true);
            }

            if (ScrollingLinks.ItemsPanelRoot != null)
            {
                ScrollingLinks.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingLinks.GetScrollViewer().ChangeView(null, 12, null, true);
            }

            if (ScrollingMusic.ItemsPanelRoot != null)
            {
                ScrollingMusic.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingMusic.GetScrollViewer().ChangeView(null, 12, null, true);
            }

            if (ScrollingVoice.ItemsPanelRoot != null)
            {
                ScrollingVoice.ItemsPanelRoot.MinHeight = e.NewSize.Height + 12;
                ScrollingVoice.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void Tab_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_tab.GetScrollViewer().VerticalOffset < 12)
            {
                _tab.GetSelector().ItemsPanelRoot.SizeChanged -= Tab_SizeChanged;
                _tab.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingMedia_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingMedia.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingMedia.ItemsPanelRoot.SizeChanged -= ScrollingMedia_SizeChanged;
                ScrollingMedia.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingFiles_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingFiles.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingFiles.ItemsPanelRoot.SizeChanged -= ScrollingFiles_SizeChanged;
                ScrollingFiles.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingLinks_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingLinks.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingLinks.ItemsPanelRoot.SizeChanged -= ScrollingLinks_SizeChanged;
                ScrollingLinks.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingMusic_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingMusic.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingMusic.ItemsPanelRoot.SizeChanged -= ScrollingMusic_SizeChanged;
                ScrollingMusic.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }

        private void ScrollingVoice_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ScrollingVoice.GetScrollViewer().VerticalOffset < 12)
            {
                ScrollingVoice.ItemsPanelRoot.SizeChanged -= ScrollingVoice_SizeChanged;
                ScrollingVoice.GetScrollViewer().ChangeView(null, 12, null, true);
            }
        }
    }

    public interface IProfileTab
    {
        int Index { get; }
        string Text { get; }

        ListViewBase GetSelector();
        ScrollViewer GetScrollViewer();

        void Update(bool embedded, bool locked);
    }
}
