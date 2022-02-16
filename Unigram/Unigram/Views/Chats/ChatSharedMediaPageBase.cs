using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Chats;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Views.Chats
{
    public class ChatSharedMediaPageBase : Page
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfileHeader Header => ProfileHeader;

        private CompositionPropertySet _properties;

        private readonly DispatcherTimer _dateHeaderTimer;
        private bool _dateHeaderCollapsed = true;

        public ChatSharedMediaPageBase()
        {
            _dateHeaderTimer = new DispatcherTimer();
            _dateHeaderTimer.Interval = TimeSpan.FromMilliseconds(2000);
            _dateHeaderTimer.Tick += (s, args) =>
            {
                _dateHeaderTimer.Stop();
                ShowHideDateHeader(false, true);
            };
        }

        #region Date visibility

        private void ShowHideDateHeader(bool show, bool animate)
        {
            if ((show && DateHeader.Visibility == Visibility.Visible) || (!show && (DateHeader.Visibility == Visibility.Collapsed || _dateHeaderCollapsed)))
            {
                return;
            }

            if (show)
            {
                _dateHeaderCollapsed = false;
            }
            else
            {
                _dateHeaderCollapsed = true;
            }

            if (!animate)
            {
                DateHeader.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            DateHeader.Visibility = Visibility.Visible;

            var visual = ElementCompositionPreview.GetElementVisual(DateHeader);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (show)
                {
                    _dateHeaderCollapsed = false;
                }
                else
                {
                    DateHeader.Visibility = Visibility.Collapsed;
                }
            };

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            visual.StartAnimation("Opacity", opacity);

            batch.End();
        }

        #endregion

        protected void InitializeSearch(TextBox field, Func<SearchMessagesFilter> filter)
        {
            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => field.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += (s, args) =>
            {
                ViewModel.Find(filter(), field.Text);
            };
        }

        #region Context menu

        private void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var message = element.Tag as Message;

            var selected = ViewModel.SelectedItems;
            if (selected.Count > 0)
            {
                if (selected.Contains(message))
                {
                    flyout.CreateFlyoutItem(ViewModel.MessagesForwardCommand, "Forward Selected", new FontIcon { Glyph = Icons.Share });

                    //if (chat.CanBeReported)
                    //{
                    //    flyout.CreateFlyoutItem(ViewModel.MessagesReportCommand, "Report Selected", new FontIcon { Glyph = Icons.ShieldError });
                    //}

                    flyout.CreateFlyoutItem(ViewModel.MessagesDeleteCommand, "Delete Selected", new FontIcon { Glyph = Icons.Delete });
                    flyout.CreateFlyoutItem(ViewModel.MessagesUnselectCommand, "Clear Selection");
                    //flyout.CreateFlyoutSeparator();
                    //flyout.CreateFlyoutItem(ViewModel.MessagesCopyCommand, "Copy Selected as Text", new FontIcon { Glyph = Icons.DocumentCopy });
                }
                else
                {
                    flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Resources.lng_context_select_msg, new FontIcon { Glyph = Icons.CheckmarkCircle });
                }
            }
            else
            {

                flyout.CreateFlyoutItem(MessageView_Loaded, ViewModel.MessageViewCommand, message, Strings.Resources.ShowInChat, new FontIcon { Glyph = Icons.Comment });
                flyout.CreateFlyoutItem(MessageDelete_Loaded, ViewModel.MessageDeleteCommand, message, Strings.Resources.Delete, new FontIcon { Glyph = Icons.Delete });
                flyout.CreateFlyoutItem(MessageForward_Loaded, ViewModel.MessageForwardCommand, message, Strings.Resources.Forward, new FontIcon { Glyph = Icons.Share });
                flyout.CreateFlyoutItem(MessageSelect_Loaded, ViewModel.MessageSelectCommand, message, Strings.Resources.lng_context_select_msg, new FontIcon { Glyph = Icons.CheckmarkCircle });
                flyout.CreateFlyoutItem(MessageSave_Loaded, ViewModel.MessageSaveCommand, message, Strings.Resources.lng_mediaview_save_as, new FontIcon { Glyph = Icons.SaveAs });
            }

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
            return true;
        }

        #endregion

        protected virtual void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
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

        private ProfileHeader _profileHeader;
        public ProfileHeader ProfileHeader => _profileHeader ??= FindName(nameof(ProfileHeader)) as ProfileHeader;

        private Grid _headerPanel;
        public Grid HeaderPanel => _headerPanel ??= FindName(nameof(HeaderPanel)) as Grid;

        private MessageService _dateHeader;
        public MessageService DateHeader => _dateHeader ??= FindName(nameof(DateHeader)) as MessageService;

        private TextBlock _dateHeaderLabel;
        public TextBlock DateHeaderLabel => _dateHeaderLabel ??= FindName(nameof(DateHeaderLabel)) as TextBlock;

        protected void List_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = ScrollingHost.GetScrollViewer();
            if (scrollingHost != null)
            {
                //ScrollingHost.ItemsPanelRoot.SizeChanged += ItemsPanelRoot_SizeChanged;
                //ScrollingHost.ItemsPanelRoot.MinHeight = ActualHeight + ProfileHeader.ActualHeight;
                Canvas.SetZIndex(ScrollingHost.ItemsPanelRoot, -1);

                var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollingHost);
                var visual = ElementCompositionPreview.GetElementVisual(HeaderPanel);

                ElementCompositionPreview.SetIsTranslationEnabled(HeaderPanel, true);

                _properties = visual.Compositor.CreatePropertySet();
                _properties.InsertScalar("ActualHeight", ProfileHeader.ActualSize.Y);

                var translation = visual.Compositor.CreateExpressionAnimation(
                    "scrollViewer.Translation.Y > -properties.ActualHeight ? 0 : -scrollViewer.Translation.Y - properties.ActualHeight");
                translation.SetReferenceParameter("scrollViewer", properties);
                translation.SetReferenceParameter("properties", _properties);

                visual.StartAnimation("Translation.Y", translation);

                //void handler(object _, object args)
                //{
                //    scrollingHost.LayoutUpdated -= handler;
                //    scrollingHost.ChangeView(null, ViewModel.VerticalOffset, null, true);
                //}

                //scrollingHost.InvalidateScrollInfo();
                //scrollingHost.ChangeView(null, ViewModel.VerticalOffset, null, true);

                //scrollingHost.LayoutUpdated += handler;
                scrollingHost.ViewChanged += OnViewChanged;
            }
        }

        private void ItemsPanelRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ScrollingHost.ItemsPanelRoot.SizeChanged -= ItemsPanelRoot_SizeChanged;

            var scrollingHost = ScrollingHost.GetScrollViewer();
            if (scrollingHost != null)
            {
                scrollingHost.ChangeView(null, ViewModel.VerticalOffset, null, true);
            }
        }

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollingHost)
            {
                ViewModel.VerticalOffset = scrollingHost.VerticalOffset;

                if (DateHeaderLabel == null)
                {
                    return;
                }

                var index = ScrollingHost.ItemsPanelRoot switch
                {
                    ItemsStackPanel stackPanel => stackPanel.FirstVisibleIndex,
                    ItemsWrapGrid wrapGrid => wrapGrid.FirstVisibleIndex,
                    _ => -1
                };

                var container = ScrollingHost.ContainerFromIndex(index) as SelectorItem;
                if (container == null || container.Tag is not Message message)
                {
                    return;
                }

                DateHeaderLabel.Text = Converter.MonthGrouping(Utils.UnixTimestampToDateTime(message.Date));

                _dateHeaderTimer.Stop();
                _dateHeaderTimer.Start();
                ShowHideDateHeader(scrollingHost.VerticalOffset > ProfileHeader.ActualHeight, true);
            }
        }

        protected void ProfileHeader_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _properties?.InsertScalar("ActualHeight", (float)e.NewSize.Height);

            //var panel = ScrollingHost.ItemsPanelRoot;
            //if (panel != null)
            //{
            //    panel.MinHeight = ActualHeight + e.NewSize.Height;
            //}
        }

        protected void Header_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ProfileItem page && page.Type != GetType())
            {
                Frame.Navigate(page.Type, null, new SuppressNavigationTransitionInfo());
            }
        }
    }
}
