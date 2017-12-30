using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Messages;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Views;
using Unigram.Core.Models;
using Unigram.Core.Services;
using Unigram.ViewModels;
using Unigram.Views.Chats;
using Unigram.Views.Users;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Profile;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI;
using Unigram.Views.Channels;
using Unigram.Themes;
using Windows.UI.Xaml.Media.Animation;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Core.Helpers;
using Unigram.Native;
using LinqToVisualTree;
using Unigram.Models;
using System.Windows.Input;
using Unigram.Views.Dialogs;

namespace Unigram.Views
{
    public sealed partial class DialogPage : Page, IMasterDetailPage
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public BindConvert Convert => BindConvert.Current;

        private double _lastKnownKeyboardHeight = 260;

        private DispatcherTimer _elapsedTimer;
        private Visual _messageVisual;
        private Visual _ellipseVisual;
        private Visual _elapsedVisual;
        private Visual _slideVisual;
        private Visual _rootVisual;

        private Visual _autocompleteLayer;
        private InsetClip _autocompleteInset;

        private Compositor _compositor;

        public DialogPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<DialogViewModel>();

            //NavigationCacheMode = NavigationCacheMode.Required;

            _typeToItemHashSetMapping.Add("UserMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ChatFriendMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("FriendMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceMessagePhotoTemplate", new HashSet<SelectorItem>());
            //_typeToItemHashSetMapping.Add("ServiceMessageLocalTemplate", new HashSet<SelectorItem>());
            //_typeToItemHashSetMapping.Add("ServiceMessageDateTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceUserCallMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("ServiceFriendCallMessageTemplate", new HashSet<SelectorItem>());
            _typeToItemHashSetMapping.Add("EmptyMessageTemplate", new HashSet<SelectorItem>());

            Messages.ChoosingItemContainer += OnChoosingItemContainer;
            Messages.ContainerContentChanging += OnContainerContentChanging;

            ViewModel.TextField = TextField;
            ViewModel.ListField = Messages;

            CheckMessageBoxEmpty();

            ViewModel.PropertyChanged += OnPropertyChanged;

            StickersPanel.StickerClick = Stickers_ItemClick;
            StickersPanel.GifClick = Gifs_ItemClick;

            Messages.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);
            StickersPanel.RegisterPropertyChangedCallback(FrameworkElement.VisibilityProperty, StickersPanel_VisibilityChanged);

            _messageVisual = ElementCompositionPreview.GetElementVisual(TextField);
            _ellipseVisual = ElementCompositionPreview.GetElementVisual(Ellipse);
            _elapsedVisual = ElementCompositionPreview.GetElementVisual(ElapsedPanel);
            _slideVisual = ElementCompositionPreview.GetElementVisual(SlidePanel);
            _rootVisual = ElementCompositionPreview.GetElementVisual(TextArea);
            _compositor = _slideVisual.Compositor;

            _ellipseVisual.CenterPoint = new Vector3(48);
            _ellipseVisual.Scale = new Vector3(0);

            _rootVisual.Clip = _compositor.CreateInsetClip(0, -100, 0, 0);

            _elapsedTimer = new DispatcherTimer();
            _elapsedTimer.Interval = TimeSpan.FromMilliseconds(100);
            _elapsedTimer.Tick += (s, args) =>
            {
                ElapsedLabel.Text = btnVoiceMessage.Elapsed.ToString("m\\:ss\\.ff");
            };

            //if (ApiInformation.IsMethodPresent("Windows.UI.Xaml.Hosting.ElementCompositionPreview", "SetImplicitShowAnimation"))
            //{
            //    var visual = ElementCompositionPreview.GetElementVisual(Header);
            //    visual.Clip = Window.Current.Compositor.CreateInsetClip();

            //    var showShowAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            //    showShowAnimation.InsertKeyFrame(0.0f, new Vector3(0, -48, 0));
            //    showShowAnimation.InsertKeyFrame(1.0f, new Vector3());
            //    showShowAnimation.Target = nameof(Visual.Offset);
            //    showShowAnimation.Duration = TimeSpan.FromMilliseconds(400);

            //    var showHideAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            //    showHideAnimation.InsertKeyFrame(0.0f, new Vector3());
            //    showHideAnimation.InsertKeyFrame(1.0f, new Vector3(0, 48, 0));
            //    showHideAnimation.Target = nameof(Visual.Offset);
            //    showHideAnimation.Duration = TimeSpan.FromMilliseconds(400);

            //    var hideHideAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            //    hideHideAnimation.InsertKeyFrame(0.0f, new Vector3());
            //    hideHideAnimation.InsertKeyFrame(1.0f, new Vector3(0, -48, 0));
            //    hideHideAnimation.Target = nameof(Visual.Offset);
            //    hideHideAnimation.Duration = TimeSpan.FromMilliseconds(400);

            //    var hideShowAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            //    hideShowAnimation.InsertKeyFrame(0.0f, new Vector3(0, 48, 0));
            //    hideShowAnimation.InsertKeyFrame(1.0f, new Vector3());
            //    hideShowAnimation.Target = nameof(Visual.Offset);
            //    hideShowAnimation.Duration = TimeSpan.FromMilliseconds(400);

            //    ElementCompositionPreview.SetImplicitShowAnimation(ManagePanel, showShowAnimation);
            //    ElementCompositionPreview.SetImplicitHideAnimation(ManagePanel, hideHideAnimation);
            //    ElementCompositionPreview.SetImplicitShowAnimation(InfoPanel, hideShowAnimation);
            //    ElementCompositionPreview.SetImplicitHideAnimation(InfoPanel, showHideAnimation);
            //}
        }

        private void TextField_LostFocus(object sender, RoutedEventArgs e)
        {
            if (StickersPanel.Visibility == Visibility.Visible && TextField.FocusState == FocusState.Unfocused)
            {
                Collapse_Click(StickersPanel, null);

                TextField.FocusMaybe(FocusState.Keyboard);
            }
            else if (ReplyMarkupPanel.Visibility == Visibility.Visible && ButtonMarkup.Visibility == Visibility.Visible && TextField.FocusState == FocusState.Unfocused)
            {
                CollapseMarkup(false);

                TextField.FocusMaybe(FocusState.Keyboard);
            }
        }

        //protected override async void OnNavigatedTo(NavigationEventArgs e)
        //{
        //    if (MainPage.TryGetPeerFromParameter(e.Parameter, out TLPeerBase peer))
        //    {
        //        DataContext = UnigramContainer.Current.ResolveType<DialogViewModel>(peer);

        //        CheckMessageBoxEmpty();

        //        ViewModel.PropertyChanged -= OnPropertyChanged;
        //        ViewModel.PropertyChanged += OnPropertyChanged;

        //        await ViewModel.OnNavigatedToAsync(TLSerializationService.Current.Deserialize((string)e.Parameter), e.NavigationMode, null);
        //    }

        //    base.OnNavigatedTo(e);
        //}

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            TextField.FocusMaybe(FocusState.Keyboard);

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            //if (_panel != null && ViewModel.With != null)
            //{
            //    var container = Messages.ContainerFromIndex(_panel.FirstVisibleIndex);
            //    if (container != null)
            //    {
            //        var peer = ViewModel.With.ToPeer();
            //        var item = Messages.ItemFromContainer(container) as TLMessageBase;

            //        ApplicationSettings.Current.AddOrUpdateValue(TLSerializationService.Current.Serialize(peer), item?.Id ?? -1);
            //    }
            //}

            base.OnNavigatingFrom(e);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Reply"))
            {
                CheckMessageBoxEmpty();
            }
        }

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ManagePanel.Visibility = Visibility.Collapsed;
                InfoPanel.Visibility = Visibility.Visible;

                ViewModel.SelectedItems = new List<TLMessageCommonBase>();
            }
            else
            {
                ManagePanel.Visibility = Visibility.Visible;
                InfoPanel.Visibility = Visibility.Collapsed;
            }
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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

            App.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            _panel = (ItemsStackPanel)Messages.ItemsPanelRoot;
            Messages.ScrollingHost.ViewChanged += OnViewChanged;

            TextField.FocusMaybe(FocusState.Keyboard);

            if (App.DataPackage != null)
            {
                var package = App.DataPackage;
                App.DataPackage = null;
                await HandlePackageAsync(package);
            }
        }

        private void Headers_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            var container = Messages.ContainerFromIndex(_panel.LastVisibleIndex) as BubbleListViewItem;
            if (container == null)
            {
                return;
            }

            var root = container.ContentTemplateRoot as Grid;
            if (root == null)
            {
                return;
            }

            var photo = root.FindName("PhotoBubble") as ProfilePicture;
            if (photo == null)
            {
                return;
            }

            var transform = photo.TransformToVisual(Messages);
            var point = transform.TransformPoint(new Point());

            var inner = VisualTreeHelper.GetChild(photo, 0) as UIElement;
            var visual = ElementCompositionPreview.GetElementVisual(inner);

            var diff = Messages.ActualHeight - (point.Y + photo.ActualHeight);
            if (diff < 0)
            {
                visual.Offset = new Vector3(0, (float)diff, 0);
            }
            else
            {
                visual.Offset = new Vector3();
            }


            Debug.WriteLine(diff);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();

            foreach (var item in _old.Values)
            {
                var presenter = item.Presenter;
                if (presenter != null && presenter.MediaPlayer != null)
                {
                    presenter.MediaPlayer.Source = null;
                    presenter.MediaPlayer.Dispose();
                    presenter.MediaPlayer = null;
                }
            }

            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;

            App.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            KeyboardPlaceholder.Height = new GridLength(args.OccludedRect.Height);
            StickersPanel.Height = args.OccludedRect.Height;
            ReplyMarkupPanel.MaxHeight = args.OccludedRect.Height;
            //ReplyMarkupViewer.MaxHeight = args.OccludedRect.Height;

            _lastKnownKeyboardHeight = Math.Max(260, args.OccludedRect.Height);

            Collapse_Click(null, null);
            CollapseMarkup(false);
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            KeyboardPlaceholder.Height = new GridLength(1, GridUnitType.Auto);
        }

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Escape && !args.KeyStatus.IsKeyReleased)
            {
                if (ViewModel.Search != null)
                {
                    ViewModel.Search = null;
                    args.Handled = true;
                }

                if (StickersPanel.Visibility == Visibility.Visible)
                {
                    if (StickersPanel.ToggleActiveView())
                    {

                    }
                    else
                    {
                        Collapse_Click(null, null);
                    }

                    args.Handled = true;
                }

                if (ReplyMarkupPanel.Visibility == Visibility.Visible && ButtonMarkup.Visibility == Visibility.Visible)
                {
                    CollapseMarkup(false);
                    args.Handled = true;
                }

                if (ViewModel.SelectionMode != ListViewSelectionMode.None)
                {
                    ViewModel.SelectionMode = ListViewSelectionMode.None;
                    args.Handled = true;
                }

                if (ViewModel.EditedMessage != null)
                {
                    ViewModel.ClearReplyCommand.Execute(null);
                    args.Handled = true;
                }

                if (args.Handled)
                {
                    Focus(FocusState.Programmatic);
                    TextField.FocusMaybe(FocusState.Keyboard);
                }
            }
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            if (ViewModel.Search != null)
            {
                ViewModel.Search = null;
                args.Handled = true;
            }

            if (StickersPanel.Visibility == Visibility.Visible)
            {
                if (StickersPanel.ToggleActiveView())
                {

                }
                else
                {
                    Collapse_Click(null, null);
                }

                args.Handled = true;
            }

            if (ReplyMarkupPanel.Visibility == Visibility.Visible && ButtonMarkup.Visibility == Visibility.Visible)
            {
                CollapseMarkup(false);
                args.Handled = true;
            }

            if (ViewModel.SelectionMode != ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
                args.Handled = true;
            }

            if (ViewModel.EditedMessage != null)
            {
                ViewModel.ClearReplyCommand.Execute(null);
                args.Handled = true;
            }

            if (args.Handled)
            {
                Focus(FocusState.Programmatic);
                TextField.FocusMaybe(FocusState.Keyboard);
            }
        }

        //private bool _isAlreadyLoading;
        //private bool _isAlreadyCalled;

        private void CheckMessageBoxEmpty()
        {
            var forwarding = false;
            if (ViewModel.Reply is TLMessagesContainter container)
            {
                forwarding = container.FwdMessages != null && container.FwdMessages.Count > 0;
            }

            if (ViewModel != null && TextField.IsEmpty && !forwarding)
            {
                btnSendMessage.Visibility = Visibility.Collapsed;
                btnCommands.Visibility = Visibility.Visible;
                btnMarkup.Visibility = Visibility.Visible;
                btnStickers.Visibility = Visibility.Visible;
                btnVoiceMessage.Visibility = Visibility.Visible;
            }
            else
            {
                btnSendMessage.Visibility = Visibility.Visible;
                btnCommands.Visibility = Visibility.Collapsed;
                btnMarkup.Visibility = Visibility.Collapsed;
                btnStickers.Visibility = Visibility.Collapsed;
                btnVoiceMessage.Visibility = Visibility.Collapsed;
            }

            if (StickersPanel.Visibility == Visibility.Visible)
            {
                Collapse_Click(StickersPanel, null);
            }

            if (ReplyMarkupPanel.Visibility == Visibility.Visible && ButtonMarkup.Visibility == Visibility.Visible)
            {
                CollapseMarkup(false);
            }
        }

        private void TextField_TextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
        {
            CheckMessageBoxEmpty();
        }

        private void TextField_TextChanged(object sender, RoutedEventArgs e)
        {
            CheckMessageBoxEmpty();
        }

        private async void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await TextField.SendAsync();
        }

        private void btnDialogInfo_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.With is TLUser user)
            {
                if (user.IsSelf)
                {
                    ViewModel.NavigationService.Navigate(typeof(DialogSharedMediaPage), ViewModel.Peer);
                }
                else
                {
                    ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), ViewModel.Peer.ToPeer());
                }
            }
            else if (ViewModel.With is TLChannel)
            {
                ViewModel.NavigationService.Navigate(typeof(ChannelDetailsPage), ViewModel.Peer.ToPeer());
            }
            else if (ViewModel.With is TLChat)
            {
                ViewModel.NavigationService.Navigate(typeof(ChatDetailsPage), ViewModel.Peer.ToPeer());
            }
        }

        private async void Attach_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.With is TLChannel channel && channel.HasBannedRights && channel.BannedRights.IsSendMedia)
            {
                if (channel.BannedRights.IsForever())
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.AttachMediaRestrictedForever, Strings.Android.AppName, Strings.Android.OK);
                }
                else
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Android.AttachMediaRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate)), Strings.Android.AppName, Strings.Android.OK);
                }

                return;
            }

            var pane = InputPane.GetForCurrentView();
            if (pane.OccludedRect != Rect.Empty)
            {
                pane.TryHide();

                // TODO: Can't find any better solution
                await Task.Delay(200);
            }

            foreach (var item in ViewModel.MediaLibrary)
            {
                item.Reset();
            }

            if (FlyoutBase.GetAttachedFlyout(ButtonAttach) is MenuFlyout flyout)
            {
                //var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
                //if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile" && (bounds.Width < 500 || bounds.Height < 500))
                //{
                //    flyout.LightDismissOverlayMode = LightDismissOverlayMode.On;
                //}
                //else
                //{
                //    flyout.LightDismissOverlayMode = LightDismissOverlayMode.Auto;
                //}

                //flyout.ShowAt(ButtonAttach, new Point(4, -4));
                flyout.ShowAt(FlyoutArea);
            }
        }

        private void AttachPickerFlyout_ItemClick(object sender, MediaSelectedEventArgs e)
        {
            var flyout = FlyoutBase.GetAttachedFlyout(ButtonAttach) as MenuFlyout;
            if (flyout != null)
            {
                flyout.Hide();
            }

            if (e.IsLocal)
            {
                ViewModel.SendMediaExecute(new ObservableCollection<StorageMedia>(ViewModel.MediaLibrary), e.Item);
            }
            else
            {
                ViewModel.SendMediaExecute(new ObservableCollection<StorageMedia> { e.Item }, e.Item);
            }
        }

        private void InlineBotResults_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SendBotInlineResult((TLBotInlineResultBase)e.ClickedItem);
        }

        #region Drag & Drop

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            await HandlePackageAsync(e.DataView);
        }
        //gridLoading.Visibility = Visibility.Visible;

        private async Task HandlePackageAsync(DataPackageView package)
        {
            var boh = string.Join(", ", package.AvailableFormats);

            if (package.Contains(StandardDataFormats.Bitmap))
            {
                var bitmap = await package.GetBitmapAsync();
                var media = new ObservableCollection<StorageMedia>();
                var cache = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\paste.jpg", CreationCollisionOption.ReplaceExisting);

                using (var stream = await bitmap.OpenReadAsync())
                using (var reader = new DataReader(stream))
                {
                    await reader.LoadAsync((uint)stream.Size);
                    var buffer = new byte[(int)stream.Size];
                    reader.ReadBytes(buffer);
                    await FileIO.WriteBytesAsync(cache, buffer);

                    media.Add(await StoragePhoto.CreateAsync(cache, true));
                }

                if (package.Contains(StandardDataFormats.Text))
                {
                    media[0].Caption = await package.GetTextAsync();
                }

                ViewModel.SendMediaExecute(media, media[0]);
            }
            else if (package.Contains(StandardDataFormats.StorageItems))
            {
                var items = await package.GetStorageItemsAsync();
                var media = new ObservableCollection<StorageMedia>();
                var files = new List<StorageFile>(items.Count);

                foreach (var file in items.OfType<StorageFile>())
                {
                    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
                    {
                        media.Add(await StoragePhoto.CreateAsync(file, true));
                    }
                    else if (file.ContentType == "video/mp4")
                    {
                        media.Add(await StorageVideo.CreateAsync(file, true));
                    }

                    files.Add(file);
                }

                // Send compressed __only__ if user is dropping photos and videos only
                if (media.Count > 0 && media.Count == files.Count)
                {
                    ViewModel.SendMediaExecute(media, media[0]);
                }
                else if (files.Count > 0)
                {
                    ViewModel.SendFileExecute(files);
                }
            }
            //else if (e.DataView.Contains(StandardDataFormats.WebLink))
            //{
            //    // TODO: Invoke getting a preview of the weblink above the Textbox
            //    var link = await e.DataView.GetWebLinkAsync();
            //    if (TextField.Text == "")
            //    {
            //        TextField.Text = link.AbsolutePath;
            //    }
            //    else
            //    {
            //        TextField.Text = (TextField.Text + " " + link.AbsolutePath);
            //    }
            //
            //    gridLoading.Visibility = Visibility.Collapsed;
            //
            //}
            else if (package.Contains(StandardDataFormats.Text))
            {
                var text = await package.GetTextAsync();

                if (package.Contains(StandardDataFormats.WebLink))
                {
                    text += Environment.NewLine + await package.GetWebLinkAsync();
                }

                TextField.Document.GetRange(TextField.Document.Selection.EndPosition, TextField.Document.Selection.EndPosition).SetText(TextSetOptions.None, text);
            }
            else if (package.Contains(StandardDataFormats.WebLink))
            {
                var text = await package.GetWebLinkAsync();
                TextField.Document.GetRange(TextField.Document.Selection.EndPosition, TextField.Document.Selection.EndPosition).SetText(TextSetOptions.None, text.ToString());
            }
        }

        #endregion

        private async void Reply_Click(object sender, RoutedEventArgs e)
        {
            var reference = sender as MessageReference;
            var message = reference.Message;

            if (message != null)
            {
                if (message is ReplyInfo replyInfo)
                {
                    message = replyInfo.Reply;
                }

                if (message is TLMessagesContainter container)
                {
                    if (container.EditMessage != null)
                    {
                        message = container.EditMessage;
                    }
                    else
                    {
                        return;
                    }
                }

                if (message is TLMessageCommonBase messageCommon)
                {
                    await ViewModel.LoadMessageSliceAsync(null, messageCommon.Id);
                }
            }
        }

        private void ReplyMarkup_ButtonClick(object sender, ReplyMarkupButtonClickEventArgs e)
        {
            var panel = sender as ReplyMarkupPanel;
            if (panel != null)
            {
                ViewModel.KeyboardButtonExecute(e.Button, panel.DataContext as TLMessage);
            }
        }

        private async void Stickers_Click(object sender, RoutedEventArgs e)
        {
            var channel = ViewModel.With as TLChannel;
            if (channel != null && channel.HasBannedRights && (channel.BannedRights.IsSendStickers || channel.BannedRights.IsSendGifs))
            {
                if (channel.BannedRights.IsForever())
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.AttachStickersRestrictedForever, Strings.Android.AppName, Strings.Android.OK);
                }
                else
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Android.AttachStickersRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate)), Strings.Android.AppName, Strings.Android.OK);
                }

                return;
            }

            VisualStateManager.GoToState(this, Window.Current.Bounds.Width < 500 ? "NarrowState" : "FilledState", false);

            if (StickersPanel.Visibility == Visibility.Collapsed)
            {
                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Programmatic);

                InputPane.GetForCurrentView().TryHide();

                StickersPanel.MinHeight = 260;
                StickersPanel.MaxHeight = 360;
                StickersPanel.Height = _lastKnownKeyboardHeight;
                StickersPanel.Visibility = Visibility.Visible;
                StickersPanel.Refresh();

                ViewModel.OpenStickersCommand.Execute(null);
            }
            else
            {
                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Keyboard);

                Collapse_Click(StickersPanel, null);
            }
        }

        private void Commands_Click(object sender, RoutedEventArgs e)
        {
            TextField.SetText("/", null);
            TextField.Focus(FocusState.Keyboard);
        }

        private void Markup_Click(object sender, RoutedEventArgs e)
        {
            if (ReplyMarkupPanel.Visibility == Visibility.Visible)
            {
                CollapseMarkup(true);
            }
            else
            {
                ShowMarkup();
            }
        }

        private void CollapseMarkup(bool keyboard)
        {
            ReplyMarkupPanel.Visibility = Visibility.Collapsed;
            ButtonMarkup.IsChecked = false;

            if (keyboard)
            {
                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Keyboard);

                InputPane.GetForCurrentView().TryShow();
            }
        }

        public void ShowMarkup()
        {
            ReplyMarkupPanel.Visibility = Visibility.Visible;
            ButtonMarkup.IsChecked = true;

            Focus(FocusState.Programmatic);
            TextField.Focus(FocusState.Programmatic);

            InputPane.GetForCurrentView().TryHide();
        }

        private void TextField_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (StickersPanel.Visibility == Visibility.Visible)
            {
                Collapse_Click(StickersPanel, null);
            }

            if (ReplyMarkupPanel.Visibility == Visibility.Visible && ButtonMarkup.Visibility == Visibility.Visible)
            {
                CollapseMarkup(false);
            }

            InputPane.GetForCurrentView().TryShow();
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            var control = sender as FrameworkElement;
            var message = control.DataContext as TLMessage;

            if (message != null && message.IsSaved())
            {
                if (message.HasFwdFrom && message.FwdFrom != null && message.FwdFrom.HasFromId)
                {
                    ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = message.FwdFrom.FromId.Value });
                }
                else if (message.HasFwdFrom && message.FwdFrom != null && message.FwdFrom.HasChannelId)
                {
                    ViewModel.NavigationService.NavigateToDialog(message.FwdFromChannel);
                }
            }
            else if (message != null && message.HasFromId)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = message.FromId.Value });
            }
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var message = button.DataContext as TLMessage;

            if (message != null && message.HasFwdFrom && message.FwdFrom != null && message.FwdFrom.HasSavedFromPeer && message.FwdFrom.SavedFromPeer != null && message.FwdFrom.HasSavedFromMsgId && message.FwdFrom.SavedFromMsgId != null)
            {
                ITLDialogWith with = null;
                if (message.FwdFrom.SavedFromPeer is TLPeerUser user)
                    with = InMemoryCacheService.Current.GetUser(user.UserId);
                else if (message.FwdFrom.SavedFromPeer is TLPeerChat chat)
                    with = InMemoryCacheService.Current.GetChat(chat.ChatId);
                else if (message.FwdFrom.SavedFromPeer is TLPeerChannel channel)
                    with = InMemoryCacheService.Current.GetChat(channel.ChannelId);

                if (with != null)
                {
                    ViewModel.NavigationService.NavigateToDialog(with, message: message.FwdFrom.SavedFromMsgId);
                }
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ViewModel.ExpandSelection(Messages.SelectedItems.Cast<TLMessageCommonBase>());
            }
        }

        #region Context menu

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var currentUser = ViewModel.With as TLUser;
            var currentChat = ViewModel.With as TLChat;
            var currentChannel = ViewModel.With as TLChannel;

            CreateFlyoutItem(ref flyout, ViewModel.SearchCommand, Strings.Android.Search);

            if (currentChannel != null && !currentChannel.IsCreator && (!currentChannel.IsMegaGroup || (currentChannel.Username != null && currentChannel.Username.Length > 0)))
            {
                CreateFlyoutItem(ref flyout, ViewModel.ReportCommand, Strings.Android.ReportChat);
            }
            if (currentUser != null)
            {
                if (ViewModel.IsShareContactAvailable)
                {
                    CreateFlyoutItem(ref flyout, ViewModel.ShareContactCommand, Strings.Android.ShareMyContactInfo);
                }
                else if (ViewModel.IsAddContactAvailable)
                {
                    CreateFlyoutItem(ref flyout, ViewModel.AddContactCommand, Strings.Android.AddToContacts);
                }
            }
            //if (this.currentEncryptedChat != null)
            //{
            //    this.timeItem2 = this.headerItem.addSubItem(13, LocaleController.getString("SetTimer", R.string.SetTimer));
            //}
            if (currentUser != null || currentChat != null || (currentChannel != null && currentChannel.IsMegaGroup && string.IsNullOrEmpty(currentChannel.Username)))
            {
                CreateFlyoutItem(ref flyout, ViewModel.DialogClearCommand, Strings.Android.ClearHistory);
            }
            if (currentUser != null)
            {
                CreateFlyoutItem(ref flyout, ViewModel.DialogDeleteCommand, Strings.Android.DeleteChatUser);
            }
            if (currentChat != null)
            {
                CreateFlyoutItem(ref flyout, ViewModel.DialogDeleteCommand, Strings.Android.DeleteAndExit);
            }
            if (currentUser != null || currentChat != null || (currentChannel != null && currentChannel.IsMegaGroup))
            {
                TLPeerNotifySettings notifySettings = null;
                if (ViewModel.Full is TLUserFull userFull)
                {
                    notifySettings = userFull.NotifySettings as TLPeerNotifySettings;
                }
                else if (ViewModel.Full is TLChatFullBase chatFull)
                {
                    notifySettings = chatFull.NotifySettings as TLPeerNotifySettings;
                }

                if (notifySettings != null)
                {
                    CreateFlyoutItem(ref flyout, ViewModel.ToggleMuteCommand, notifySettings.IsMuted ? Strings.Android.UnmuteNotifications : Strings.Android.MuteNotifications);
                }
            }

            //if (currentUser == null || !currentUser.IsSelf)
            //{
            //    this.muteItem = this.headerItem.addSubItem(18, null);
            //}
            //else if (currentUser.IsSelf)
            //{
            //    CreateFlyoutItem(ref flyout, null, Strings.Android.AddShortcut);
            //}
            if (currentUser != null && currentUser.IsBot && ViewModel.Full is TLUserFull botFull && botFull.HasBotInfo)
            {
                if (botFull.BotInfo.Commands.Any(x => x.Command.Equals("settings")))
                {
                    CreateFlyoutItem(ref flyout, null, Strings.Android.BotSettings);
                }

                if (botFull.BotInfo.Commands.Any(x => x.Command.Equals("help")))
                {
                    CreateFlyoutItem(ref flyout, null, Strings.Android.BotHelp);
                }
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt((Button)sender);
            }
        }

        private void Message_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var messageCommon = element.DataContext as TLMessageCommonBase;
            var channel = messageCommon.Parent as TLChannel;

            if (messageCommon is TLMessageService serviceMessage && (serviceMessage.Action is TLMessageActionDate || serviceMessage.Action is TLMessageActionUnreadMessages))
            {
                return;
            }

            // Generic
            CreateFlyoutItem(ref flyout, MessageReply_Loaded, ViewModel.MessageReplyCommand, messageCommon, Strings.Android.Reply);
            CreateFlyoutItem(ref flyout, MessagePin_Loaded, ViewModel.MessagePinCommand, messageCommon, ViewModel.PinnedMessage?.Id == messageCommon.Id ? Strings.Android.UnpinMessage : Strings.Android.PinMessage);
            CreateFlyoutItem(ref flyout, MessageEdit_Loaded, ViewModel.MessageEditCommand, messageCommon, Strings.Android.Edit);
            CreateFlyoutItem(ref flyout, MessageForward_Loaded, ViewModel.MessageForwardCommand, messageCommon, Strings.Android.Forward);
            CreateFlyoutItem(ref flyout, MessageDelete_Loaded, ViewModel.MessageDeleteCommand, messageCommon, Strings.Android.Delete);
            CreateFlyoutItem(ref flyout, MessageSelect_Loaded, ViewModel.MessageSelectCommand, messageCommon, Strings.Resources.Select);
            CreateFlyoutItem(ref flyout, MessageCopy_Loaded, ViewModel.MessageCopyCommand, messageCommon, Strings.Android.Copy);
            CreateFlyoutItem(ref flyout, MessageCopyMedia_Loaded, ViewModel.MessageCopyMediaCommand, messageCommon, Strings.Resources.CopyImage);
            CreateFlyoutItem(ref flyout, MessageCopyLink_Loaded, ViewModel.MessageCopyLinkCommand, messageCommon, Strings.Android.CopyLink);

            // Stickers
            CreateFlyoutItem(ref flyout, MessageAddSticker_Loaded, new RelayCommand(() => Sticker_Click(element, null)), messageCommon, Strings.Android.AddToStickers);
            CreateFlyoutItem(ref flyout, MessageFaveSticker_Loaded, ViewModel.MessageFaveStickerCommand, messageCommon, Strings.Android.AddToFavorites);
            CreateFlyoutItem(ref flyout, MessageUnfaveSticker_Loaded, ViewModel.MessageUnfaveStickerCommand, messageCommon, Strings.Android.DeleteFromFavorites);

            CreateFlyoutItem(ref flyout, MessageSaveGIF_Loaded, ViewModel.MessageSaveGIFCommand, messageCommon, Strings.Android.SaveToGIFs);
            CreateFlyoutItem(ref flyout, MessageSaveMedia_Loaded, ViewModel.MessageSaveMediaCommand, messageCommon, Strings.Resources.SaveAs);

            CreateFlyoutItem(ref flyout, MessageAddContact_Loaded, ViewModel.MessageAddContactCommand, messageCommon, Strings.Android.AddContactTitle);
            //CreateFlyoutItem(ref flyout, MessageSaveDownload_Loaded, ViewModel.MessageSaveDownloadCommand, messageCommon, Strings.Android.SaveToDownloads);

            //sender.ContextFlyout = menu;

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, Func<TLMessageCommonBase, Visibility> visibility, ICommand command, object parameter, string text)
        {
            var value = visibility(parameter as TLMessageCommonBase);
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

        private void CreateFlyoutItem(ref MenuFlyout flyout, ICommand command, string text)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = command != null;
            flyoutItem.Command = command;
            flyoutItem.Text = text;

            flyout.Items.Add(flyoutItem);
        }

        private Visibility MessageReply_Loaded(TLMessageCommonBase messageCommon)
        {
            //var channel = ViewModel.With as TLChannel;
            //if (channel != null && channel.MigratedFromChatId != null)
            //{
            //    if (messageCommon.ToId is TLPeerChat)
            //    {
            //        element.Visibility = messageCommon.ToId.Id == channel.MigratedFromChatId ? Visibility.Collapsed : Visibility.Visible;
            //    }
            //}

            if (messageCommon.Parent is TLChannel channel)
            {
                if (channel.IsBroadcast)
                {
                    return channel.IsCreator || channel.HasAdminRights ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        private Visibility MessagePin_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message && message.Parent is TLChannel channel && (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsPinMessages)))
            {
                if (message.ToId is TLPeerChannel)
                {
                    //element.Text = ViewModel.PinnedMessage != null && ViewModel.PinnedMessage.Id == messageCommon.Id ? "Unpin message" : "Pin message";
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageEdit_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message)
            {
                if (message.IsRoundVideo() || message.IsSticker())
                {
                    return Visibility.Collapsed;
                }

                var channel = message.Parent as TLChannel;
                if (message.IsOut && message.ToId is TLPeerUser userPeer && userPeer.Id == SettingsHelper.UserId)
                {
                    return Visibility.Visible;
                }
                else if (message.HasFwdFrom == false && message.ViaBotId == null && (message.IsOut || (channel != null && channel.IsBroadcast && (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsEditMessages)))) && (message.Media is ITLMessageMediaCaption || message.Media is TLMessageMediaWebPage || message.Media is TLMessageMediaEmpty || message.Media == null))
                {
                    var date = TLUtils.DateToUniversalTimeTLInt(ViewModel.ProtoService.ClientTicksDelta, DateTime.Now);
                    var config = ViewModel.CacheService.GetConfig();
                    if (config != null && message.Date + config.EditTimeLimit < date)
                    {
                        return Visibility.Collapsed;
                    }

                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageDelete_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon.Parent is TLChannel channel)
            {
                if (messageCommon.Id == 1 && messageCommon.ToId is TLPeerChannel)
                {
                    return Visibility.Collapsed;
                }

                if (!messageCommon.IsOut && !channel.IsCreator && !channel.HasAdminRights || (channel.AdminRights != null && !channel.AdminRights.IsDeleteMessages))
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Visible;
        }

        private Visibility MessageForward_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message)
            {
                if (message.Media is TLMessageMediaPhoto photoMedia)
                {
                    return photoMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (message.Media is TLMessageMediaDocument documentMedia)
                {
                    return documentMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }

                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageCopy_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message)
            {
                if (!string.IsNullOrEmpty(message.Message))
                {
                    return Visibility.Visible;
                }

                if (message.Media is ITLMessageMediaCaption mediaCaption && !string.IsNullOrEmpty(mediaCaption.Caption))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageCopyMedia_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message)
            {
                if (message.Media is TLMessageMediaPhoto photoMedia)
                {
                    return photoMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    return webPage.HasPhoto ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageCopyLink_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon.Parent is TLChannel channel && channel.HasUsername)
            {
                //element.Text = channel.IsBroadcast ? "Copy post link" : "Copy message link";
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageSelect_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message)
            {
                if (message.Media is TLMessageMediaPhoto photoMedia)
                {
                    return photoMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (message.Media is TLMessageMediaDocument documentMedia)
                {
                    return documentMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }

                return ViewModel.SelectionMode == ListViewSelectionMode.None ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageAddSticker_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message && message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                if (document.StickerSet is TLInputStickerSetID setId)
                {
                    return ViewModel.Stickers.StickersService.IsStickerPackInstalled(setId.Id) ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    return Visibility.Collapsed;
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageSaveSticker_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message && message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document && document.StickerSet is TLInputStickerSetID setId)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageFaveSticker_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message && message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document && document.StickerSet is TLInputStickerSetID setId)
            {
                return ViewModel.Stickers.StickersService.IsStickerInFavorites(document) ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageUnfaveSticker_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message && message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document && document.StickerSet is TLInputStickerSetID setId)
            {
                return ViewModel.Stickers.StickersService.IsStickerInFavorites(document) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageSaveMedia_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message)
            {
                if (message.Media is TLMessageMediaPhoto photoMedia)
                {
                    return photoMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (message.Media is TLMessageMediaDocument documentMedia)
                {
                    return documentMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    return webPage.HasDocument || webPage.HasPhoto ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageSaveDownload_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message)
            {
                if (message.Media is TLMessageMediaPhoto photoMedia)
                {
                    return photoMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (message.Media is TLMessageMediaDocument documentMedia)
                {
                    return documentMedia.HasTTLSeconds ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    return webPage.HasDocument || webPage.HasPhoto ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageSaveGIF_Loaded(TLMessageCommonBase messageCommon)
        {
            if (messageCommon is TLMessage message)
            {
                if (message.IsGif())
                {
                    return Visibility.Visible;
                }
                else if (message.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
                {
                    if (TLMessage.IsGif(webPage.Document))
                    {
                        return Visibility.Visible;
                    }
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageCallAgain_Loaded(TLMessageCommonBase messageCommon)
        {
            var message = messageCommon as TLMessageService;
            if (message != null)
            {
                if (message.Action is TLMessageActionPhoneCall)
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        private Visibility MessageAddContact_Loaded(TLMessageCommonBase messageCommon)
        {
            var message = messageCommon as TLMessage;
            if (message != null)
            {
                if (message.Media is TLMessageMediaContact contactMedia && contactMedia.User is TLUser user && user.Id > 0)
                {
                    return user.IsContact ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        #endregion

        private void Media_Click(object sender, RoutedEventArgs e)
        {
            Media.Download_Click(sender as FrameworkElement, null);
        }

        private void Download_Click(object sender, TransferCompletedEventArgs e)
        {
            Media.Download_Click(sender as FrameworkElement, e);
        }

        public async void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ViewModel.With is TLChannel channel && channel.HasBannedRights && channel.BannedRights != null && channel.BannedRights.IsSendStickers)
            {
                if (channel.BannedRights.IsForever())
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.AttachStickersRestrictedForever, Strings.Android.AppName, Strings.Android.OK);
                }
                else
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Android.AttachStickersRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate)), Strings.Android.AppName, Strings.Android.OK);
                }

                return;
            }

            ViewModel.SendStickerCommand.Execute(e.ClickedItem);
            ViewModel.StickerPack = null;
            TextField.SetText(null, null);
            Collapse_Click(null, new RoutedEventArgs());

            TextField.FocusMaybe(FocusState.Keyboard);
        }

        public async void Gifs_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (ViewModel.With is TLChannel channel && channel.HasBannedRights && channel.BannedRights.IsSendGifs)
            {
                if (channel.BannedRights.IsForever())
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.AttachStickersRestrictedForever, Strings.Android.AppName, Strings.Android.OK);
                }
                else
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Android.AttachStickersRestricted, BindConvert.Current.BannedUntil(channel.BannedRights.UntilDate)), Strings.Android.AppName, Strings.Android.OK);
                }

                return;
            }

            // I'd like to move this to StickersView
            var document = e.ClickedItem as TLDocument;
            if (document == null && e.ClickedItem is MosaicMediaPosition position)
            {
                document = position.Item as TLDocument;
            }

            ViewModel.SendGifCommand.Execute(document);
            ViewModel.StickerPack = null;
            TextField.SetText(null, null);
            Collapse_Click(null, new RoutedEventArgs());

            TextField.FocusMaybe(FocusState.Keyboard);
        }

        private async void Sticker_Click(object sender, RoutedEventArgs e)
        {
            var element = sender as FrameworkElement;
            var message = element.DataContext as TLMessage;

            if (message?.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var stickerAttribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (stickerAttribute != null && stickerAttribute.StickerSet.TypeId != TLType.InputStickerSetEmpty)
                {
                    await StickerSetView.Current.ShowAsync(stickerAttribute.StickerSet, Stickers_ItemClick);
                }
            }
        }

        private async void DatePickerFlyout_DatePicked(DatePickerFlyout sender, DatePickedEventArgs args)
        {
            //var offset = TLUtils.DateToUniversalTimeTLInt(ViewModel.ProtoService.ClientTicksDelta, args.NewDate.Date);
            //await ViewModel.LoadDateSliceAsync(offset);
        }

        private void TextArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _rootVisual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }

        private void ElapsedPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var point = _elapsedVisual.Offset;
            point.X = (float)-e.NewSize.Width;

            _elapsedVisual.Offset = point;
            _elapsedVisual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }

        private void SlidePanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var point = _slideVisual.Offset;
            point.X = (float)e.NewSize.Width + 36;

            _slideVisual.Opacity = 0;
            _slideVisual.Offset = point;
            _slideVisual.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
        }

        private void VoiceButton_RecordingStarted(object sender, EventArgs e)
        {
            var slideWidth = (float)SlidePanel.ActualWidth;
            var elapsedWidth = (float)ElapsedPanel.ActualWidth;

            _slideVisual.Opacity = 1;

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var messageAnimation = _compositor.CreateScalarKeyFrameAnimation();
            messageAnimation.InsertKeyFrame(0, 0);
            messageAnimation.InsertKeyFrame(1, 48);
            messageAnimation.Duration = TimeSpan.FromMilliseconds(300);

            AttachTextAreaExpression();

            var slideAnimation = _compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, slideWidth + 36);
            slideAnimation.InsertKeyFrame(1, 0);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var elapsedAnimation = _compositor.CreateScalarKeyFrameAnimation();
            elapsedAnimation.InsertKeyFrame(0, -elapsedWidth);
            elapsedAnimation.InsertKeyFrame(1, 0);
            elapsedAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var ellipseAnimation = _compositor.CreateVector3KeyFrameAnimation();
            ellipseAnimation.InsertKeyFrame(0, new Vector3(56f / 96f));
            ellipseAnimation.InsertKeyFrame(1, new Vector3(1));
            ellipseAnimation.Duration = TimeSpan.FromMilliseconds(200);

            _messageVisual.StartAnimation("Offset.Y", messageAnimation);
            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _elapsedVisual.StartAnimation("Offset.X", elapsedAnimation);
            _ellipseVisual.StartAnimation("Scale", ellipseAnimation);

            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Start();

                AttachExpression();
                //DetachTextAreaExpression();
            };
            batch.End();

            ViewModel.OutputTypingManager.SetTyping(btnVoiceMessage.IsChecked.Value ? (TLSendMessageActionBase)new TLSendMessageRecordRoundAction() : new TLSendMessageRecordAudioAction());
        }

        private void VoiceButton_RecordingStopped(object sender, EventArgs e)
        {
            AttachExpression();
            AttachTextAreaExpression();

            var slidePosition = (float)(LayoutRoot.ActualWidth - 48 - 36);
            var difference = (float)(slidePosition - ElapsedPanel.ActualWidth);

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var slideAnimation = _compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, _slideVisual.Offset.X);
            slideAnimation.InsertKeyFrame(1, -slidePosition);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(200);

            var messageAnimation = _compositor.CreateScalarKeyFrameAnimation();
            messageAnimation.InsertKeyFrame(0, 48);
            messageAnimation.InsertKeyFrame(1, 0);
            messageAnimation.Duration = TimeSpan.FromMilliseconds(200);

            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _messageVisual.StartAnimation("Offset.Y", messageAnimation);

            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Stop();

                DetachExpression();
                //DetachTextAreaExpression();

                ElapsedLabel.Text = "0:00,0";

                var point = _slideVisual.Offset;
                point.X = _slideVisual.Size.X + 36;

                _slideVisual.Opacity = 0;
                _slideVisual.Offset = point;

                point = _elapsedVisual.Offset;
                point.X = -_elapsedVisual.Size.X;

                _elapsedVisual.Offset = point;
            };
            batch.End();

            ViewModel.OutputTypingManager.CancelTyping();
        }

        private void VoiceButton_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var cumulative = (float)e.Cumulative.Translation.X;
            var point = _slideVisual.Offset;
            point.X = Math.Min(0, cumulative);

            _slideVisual.Offset = point;
        }

        private void AttachExpression()
        {
            var elapsedExpression = _compositor.CreateExpressionAnimation("min(0, slide.Offset.X + ((root.Size.X - 48 - 36 - slide.Size.X) - elapsed.Size.X))");
            elapsedExpression.SetReferenceParameter("slide", _slideVisual);
            elapsedExpression.SetReferenceParameter("elapsed", _elapsedVisual);
            elapsedExpression.SetReferenceParameter("root", _rootVisual);

            var ellipseExpression = _compositor.CreateExpressionAnimation("Vector3(max(0, 1 + slide.Offset.X / (root.Size.X - 48 - 36)), max(0, 1 + slide.Offset.X / (root.Size.X - 48 - 36)), 1)");
            ellipseExpression.SetReferenceParameter("slide", _slideVisual);
            ellipseExpression.SetReferenceParameter("elapsed", _elapsedVisual);
            ellipseExpression.SetReferenceParameter("root", _rootVisual);

            _elapsedVisual.StopAnimation("Offset.X");
            _elapsedVisual.StartAnimation("Offset.X", elapsedExpression);

            _ellipseVisual.StopAnimation("Scale");
            _ellipseVisual.StartAnimation("Scale", ellipseExpression);
        }

        private void DetachExpression()
        {
            _elapsedVisual.StopAnimation("Offset.X");
            _ellipseVisual.StopAnimation("Scale");
        }

        private void AttachTextAreaExpression()
        {
            AttachTextAreaExpression(ButtonAttach);
            AttachTextAreaExpression(ButtonSilent);
            AttachTextAreaExpression(btnCommands);
            AttachTextAreaExpression(btnStickers);
            AttachTextAreaExpression(btnEditMessage);
            AttachTextAreaExpression(btnSendMessage);
        }

        private void AttachTextAreaExpression(FrameworkElement element)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);

            var expression = _compositor.CreateExpressionAnimation("visual.Offset.Y");
            expression.SetReferenceParameter("visual", _messageVisual);

            visual.StopAnimation("Offset.Y");
            visual.StartAnimation("Offset.Y", expression);
        }

        private void DetachTextAreaExpression()
        {
            DetachTextAreaExpression(ButtonAttach);
            DetachTextAreaExpression(ButtonSilent);
            DetachTextAreaExpression(btnCommands);
            DetachTextAreaExpression(btnStickers);
            DetachTextAreaExpression(btnEditMessage);
            DetachTextAreaExpression(btnSendMessage);
        }

        private void DetachTextAreaExpression(FrameworkElement element)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            visual.StopAnimation("Offset.Y");
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            TextField.Document.GetText(TextGetOptions.None, out string hidden);
            TextField.Document.GetText(TextGetOptions.NoHidden, out string text);

            if (e.ClickedItem is TLUser user && BubbleTextBox.SearchByUsername(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string username))
            {
                var insert = string.Empty;
                var adjust = 0;

                if (user.HasUsername)
                {
                    insert = user.Username;
                }
                else
                {
                    insert = user.HasFirstName ? user.FirstName : user.LastName;
                    adjust = 1;
                }

                var format = TextField.Document.GetDefaultCharacterFormat();
                var start = TextField.Document.Selection.StartPosition - username.Length - adjust + insert.Length;
                var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - username.Length - adjust, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                if (user.HasUsername == false)
                {
                    //range.CharacterFormat.Underline = UnderlineType.Dash;
                    range.CharacterFormat.ForegroundColor = Colors.Red;
                    range.Link = $"\"{user.Id}\"";
                    start += range.Link.Length + "HYPERLINK ".Length;
                }

                TextField.Document.GetRange(start, start).SetText(TextSetOptions.None, " ");
                TextField.Document.Selection.StartPosition = start + 1;
                TextField.Document.SetDefaultCharacterFormat(format);

                ViewModel.Autocomplete = null;
            }
            else if (e.ClickedItem is TLUserCommand command)
            {
                var insert = $"/{command.Item.Command}";
                if (command.User.HasUsername && (ViewModel.With is TLChannel || ViewModel.With is TLChat))
                {
                    insert += $"@{command.User.Username}";
                }

                TextField.SetText(null, null);
                ViewModel.SendCommand.Execute(insert);

                ViewModel.Autocomplete = null;
            }
            else if (e.ClickedItem is EmojiSuggestion emoji && BubbleTextBox.SearchByEmoji(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string replacement))
            {
                var insert = $"{emoji.Emoji} ";
                var start = TextField.Document.Selection.StartPosition - 1 - replacement.Length + insert.Length;
                var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - 1 - replacement.Length, TextField.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                //TextField.Document.GetRange(start, start).SetText(TextSetOptions.None, " ");
                //TextField.Document.Selection.StartPosition = start + 1;
                TextField.Document.Selection.StartPosition = start;

                ViewModel.Autocomplete = null;
            }
        }

        #region Binding

        public Visibility ConvertBotInfo(TLBotInfo info, bool last)
        {
            return info != null && !string.IsNullOrEmpty(info.Description) && last ? Visibility.Visible : Visibility.Collapsed;
        }

        public Visibility ConvertIsEmpty(bool empty, bool self, bool bot, bool should)
        {
            if (should)
            {
                return empty && self ? Visibility.Visible : Visibility.Collapsed;
            }

            return empty && !self && !bot ? Visibility.Visible : Visibility.Collapsed;
        }

        public string ConvertEmptyText(int userId)
        {
            return userId != 777000 && userId != 429000 && userId != 4244000 && (userId / 1000 == 333 || userId % 1000 == 0) ? Strings.Android.GotAQuestion : Strings.Android.NoMessages;
        }

        public string ConvertSelectedCount(int count, bool items)
        {
            if (items)
            {
                // TODO: Send 1 Photo/Video
                return count > 0 ? string.Format(Strings.Android.SendItems, count) : Strings.Android.ChatGallery;
            }
            else
            {
                return count > 0 ? count > 1 ? Strings.Android.SendAsFiles : Strings.Android.SendAsFile : Strings.Android.ChatDocument;
            }
        }

        private string ConvertPlaceholder(ITLDialogWith with)
        {
            if (with is TLChannel channel && channel.IsBroadcast)
            {
                return Strings.Android.ChannelBroadcast;
            }

            return Strings.Android.TypeMessage;
        }

        private string ConvertReportSpam(ITLDialogWith with)
        {
            if (with is TLUser)
            {
                return Strings.Android.ReportSpam;
            }

            return Strings.Android.ReportSpamAndLeave;
        }

        #endregion

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            if (button.DataContext is TLMessage message)
            {
                ViewModel.MessageShareCommand.Execute(message);
            }
        }

        private async void Date_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            if (button.DataContext is TLMessageCommonBase message)
            {
                var dialog = new Controls.Views.CalendarView();
                dialog.MaxDate = DateTimeOffset.Now.Date;
                dialog.SelectedDates.Add(BindConvert.Current.DateTime(message.Date));

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary && dialog.SelectedDates.Count > 0)
                {
                    var offset = TLUtils.DateToUniversalTimeTLInt(ViewModel.ProtoService.ClientTicksDelta, dialog.SelectedDates.FirstOrDefault().Date);
                    await ViewModel.LoadDateSliceAsync(offset);
                }
            }
        }

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            if (HeaderOverlay.Visibility == Visibility.Visible)
            {
                StickersPanel.MinHeight = 260;
                StickersPanel.MaxHeight = 360;
                StickersPanel.Height = _lastKnownKeyboardHeight;
                ButtonExpand.Glyph = "\uE010";

                HeaderOverlay.Visibility = Visibility.Collapsed;
                UnmaskTitleAndStatusBar();
            }
            else
            {
                StickersPanel.MinHeight = ActualHeight - 48 * 2;
                StickersPanel.MaxHeight = ActualHeight - 48 * 2;
                StickersPanel.Height = double.NaN;
                ButtonExpand.Glyph = "\uE011";

                HeaderOverlay.Visibility = Visibility.Visible;
                MaskTitleAndStatusBar();
            }
        }

        private void Collapse_Click(object sender, RoutedEventArgs e)
        {
            if ((HeaderOverlay.Visibility == Visibility.Visible && sender == null) || e != null)
            {
                StickersPanel.MinHeight = 260;
                StickersPanel.MaxHeight = 360;
                StickersPanel.Height = _lastKnownKeyboardHeight;
                ButtonExpand.Glyph = "\uE010";

                HeaderOverlay.Visibility = Visibility.Collapsed;
                UnmaskTitleAndStatusBar();
            }
            else
            {
                StickersPanel.MinHeight = 260;
                StickersPanel.MaxHeight = 360;
                StickersPanel.Height = _lastKnownKeyboardHeight;
                ButtonExpand.Glyph = "\uE010";

                HeaderOverlay.Visibility = Visibility.Collapsed;
                UnmaskTitleAndStatusBar();

                StickersPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void StickersPanel_VisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (StickersPanel.Visibility == Visibility.Collapsed)
            {
                HeaderOverlay.Visibility = Visibility.Collapsed;
                UnmaskTitleAndStatusBar();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (HeaderOverlay.Visibility == Visibility.Visible)
            {
                StickersPanel.MinHeight = e.NewSize.Height - 48 * 2;
                StickersPanel.MaxHeight = e.NewSize.Height - 48 * 2;
            }
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var height = ListAutocomplete.ItemsPanelRoot.ActualHeight;
            var padding = ListAutocomplete.ActualHeight - Math.Min(154, ListAutocomplete.Items.Count * 44);

            //ListAutocomplete.Padding = new Thickness(0, padding, 0, 0);
            AutocompleteHeader.Margin = new Thickness(0, padding, 0, -height);
            AutocompleteHeader.Height = height;

            Debug.WriteLine("Autocomplete size changed");
        }

        private void MaskTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["TelegramBackgroundTitlebarBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;
            var overlayBrush = new SolidColorBrush(Color.FromArgb(0x99, 0x00, 0x00, 0x00));

            if (overlayBrush != null)
            {
                var maskBackground = ColorsHelper.AlphaBlend(backgroundBrush.Color, overlayBrush.Color);
                var maskForeground = ColorsHelper.AlphaBlend(foregroundBrush.Color, overlayBrush.Color);

                titlebar.BackgroundColor = maskBackground;
                titlebar.ForegroundColor = maskForeground;
                titlebar.ButtonBackgroundColor = maskBackground;
                titlebar.ButtonForegroundColor = maskForeground;

                if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
                {
                    var statusBar = StatusBar.GetForCurrentView();
                    statusBar.BackgroundColor = maskBackground;
                    statusBar.ForegroundColor = maskForeground;
                }
            }
        }

        private void UnmaskTitleAndStatusBar()
        {
            var titlebar = ApplicationView.GetForCurrentView().TitleBar;
            var backgroundBrush = Application.Current.Resources["TelegramBackgroundTitlebarBrush"] as SolidColorBrush;
            var foregroundBrush = Application.Current.Resources["SystemControlForegroundBaseHighBrush"] as SolidColorBrush;

            titlebar.BackgroundColor = backgroundBrush.Color;
            titlebar.ForegroundColor = foregroundBrush.Color;
            titlebar.ButtonBackgroundColor = backgroundBrush.Color;
            titlebar.ButtonForegroundColor = foregroundBrush.Color;

            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var statusBar = StatusBar.GetForCurrentView();
                statusBar.BackgroundColor = backgroundBrush.Color;
                statusBar.ForegroundColor = foregroundBrush.Color;
            }
        }

        private void Autocomplete_Loaded(object sender, RoutedEventArgs e)
        {
            var padding = ActualHeight - 48 * 2 - 152;

            var boh = ListAutocomplete.Descendants().FirstOrDefault();

            _autocompleteLayer = ElementCompositionPreview.GetElementVisual(ListAutocomplete);
            _autocompleteLayer.Clip = _autocompleteInset = _compositor.CreateInsetClip(0, (float)padding, 0, 0);

            var scroll = ListAutocomplete.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;
            if (scroll != null)
            {
                //_scrollingHost = scroll;
                //_scrollingHost.ChangeView(null, 0, null, true);
                //scroll.ViewChanged += Scroll_ViewChanged;
                //Scroll_ViewChanged(scroll, null);

                //var brush = App.Current.Resources["SystemControlBackgroundChromeMediumLowBrush"] as SolidColorBrush;
                var props = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scroll);

                //if (_backgroundVisual == null)
                //{
                //    _backgroundVisual = ElementCompositionPreview.GetElementVisual(BackgroundPanel).Compositor.CreateSpriteVisual();
                //    ElementCompositionPreview.SetElementChildVisual(BackgroundPanel, _backgroundVisual);
                //}

                //_backgroundVisual.Brush = _backgroundVisual.Compositor.CreateColorBrush(brush.Color);
                //_backgroundVisual.Size = new System.Numerics.Vector2((float)BackgroundPanel.ActualWidth, (float)BackgroundPanel.ActualHeight);
                //_backgroundVisual.Clip = _backgroundVisual.Compositor.CreateInsetClip();

                //_expression = _expression ?? _backgroundVisual.Compositor.CreateExpressionAnimation("Max(Maximum, Scrolling.Translation.Y)");
                //_expression.SetReferenceParameter("Scrolling", props);
                //_expression.SetScalarParameter("Maximum", -(float)BackgroundPanel.Margin.Top + 1);
                //_backgroundVisual.StopAnimation("Offset.Y");
                //_backgroundVisual.StartAnimation("Offset.Y", _expression);


                ExpressionAnimation _expressionClip = null;
                //_expressionClip = _expressionClip ?? _compositor.CreateExpressionAnimation("Min(0, Maximum - Scrolling.Translation.Y)");
                _expressionClip = _expressionClip ?? _compositor.CreateExpressionAnimation("Scrolling.Translation.Y");
                _expressionClip.SetReferenceParameter("Scrolling", props);
                _expressionClip.SetScalarParameter("Maximum", -(float)padding);
                _autocompleteLayer.Clip.StopAnimation("Offset.Y");
                _autocompleteLayer.Clip.StartAnimation("Offset.Y", _expressionClip);
            }

            //var panel = List.ItemsPanelRoot as ItemsWrapGrid;
            //if (panel != null)
            //{
            //    panel.SizeChanged += (s, args) =>
            //    {
            //        Scroll_ViewChanged(scroll, null);
            //    };
            //}
        }

        private void Mentions_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            ViewModel.ReadMentionsCommand.Execute();
        }

        private void ItemsStackPanel_Loading(FrameworkElement sender, object args)
        {
            Messages.SetScrollMode();
        }

        private void ServiceMessage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var message = button.DataContext as TLMessageService;

            if (message == null)
            {
                return;
            }

            ViewModel.MessageServiceCommand.Execute(message);
        }
    }

    public class MediaLibraryCollection : IncrementalCollection<StorageMedia>, ISupportIncrementalLoading
    {
        public StorageFileQueryResult Query { get; private set; }
        public uint StartIndex { get; private set; }

        public MediaLibraryCollection()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
            {
                return;
            }

            var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, Constants.MediaTypes);
            queryOptions.FolderDepth = FolderDepth.Deep;

            Query = KnownFolders.PicturesLibrary.CreateFileQueryWithOptions(queryOptions);
            Query.ContentsChanged += OnContentsChanged;
            StartIndex = 0;
        }

        private int _selectedCount;
        public int SelectedCount
        {
            get
            {
                return _selectedCount;
            }
        }

        private void OnContentsChanged(IStorageQueryResultBase sender, object args)
        {
            Execute.BeginOnUIThread(() =>
            {
                StartIndex = 0;
                Clear();
                UpdateCount();
            });
        }

        public override async Task<IList<StorageMedia>> LoadDataAsync()
        {
            var items = new List<StorageMedia>();
            uint resultCount = 0;
            var result = await Query.GetFilesAsync(StartIndex, 10);
            StartIndex += (uint)result.Count;

            resultCount = (uint)result.Count;

            foreach (var file in result)
            {
                var storage = await StorageMedia.CreateAsync(file, false);
                if (storage != null)
                {
                    items.Add(storage);
                    storage.PropertyChanged += OnPropertyChanged;
                }
            }

            return items;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("IsSelected"))
            {
                UpdateCount();
            }
        }

        private void UpdateCount()
        {
            _selectedCount = this.Count(x => x.IsSelected);
            OnPropertyChanged(new PropertyChangedEventArgs("SelectedCount"));
        }
    }

    //public class MediaLibraryCollection : IncrementalCollection<StorageMedia>, ISupportIncrementalLoading
    //{
    //    public StorageLibrary Library => _library;
    //    public StorageFileQueryResult Query => _query;

    //    private readonly StorageMediaComparer _comparer;

    //    private StorageLibrary _library;
    //    private StorageFileQueryResult _query;

    //    private uint _startIndex;

    //    public MediaLibraryCollection()
    //    {
    //        if (Windows.ApplicationModel.DesignMode.DesignModeEnabled)
    //        {
    //            return;
    //        }

    //        _comparer = new StorageMediaComparer();
    //    }

    //    private int _selectedCount;
    //    public int SelectedCount
    //    {
    //        get
    //        {
    //            return _selectedCount;
    //        }
    //    }

    //    private async void OnContentsChanged(IStorageQueryResultBase sender, object args)
    //    {
    //        var reader = _library.ChangeTracker.GetChangeReader();
    //        var changes = await reader.ReadBatchAsync();

    //        foreach (StorageLibraryChange change in changes)
    //        {
    //            if (change.ChangeType == StorageLibraryChangeType.ChangeTrackingLost)
    //            {
    //                // Change tracker is in an invalid state and must be reset
    //                // This should be a very rare case, but must be handled
    //                Library.ChangeTracker.Reset();
    //                return;
    //            }
    //            if (change.IsOfType(StorageItemTypes.File))
    //            {
    //                await ProcessFileChange(change);
    //            }
    //            else if (change.IsOfType(StorageItemTypes.Folder))
    //            {
    //                // No-op; not interested in folders
    //            }
    //            else
    //            {
    //                if (change.ChangeType == StorageLibraryChangeType.Deleted)
    //                {
    //                    //UnknownItemRemoved(change.Path);
    //                }
    //            }
    //        }

    //        // Mark that all the changes have been seen and for the change tracker
    //        // to never return these changes again
    //        await reader.AcceptChangesAsync();
    //    }

    //    private async Task ProcessFileChange(StorageLibraryChange change)
    //    {
    //        switch (change.ChangeType)
    //        {
    //            // New File in the Library
    //            case StorageLibraryChangeType.Created:
    //            case StorageLibraryChangeType.MovedIntoLibrary:
    //            case StorageLibraryChangeType.MovedOrRenamed:
    //                if (Constants.MediaTypes.Any(x => change.Path.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
    //                {
    //                    var file = (StorageFile)(await change.GetStorageItemAsync());

    //                    Execute.BeginOnUIThread(async () =>
    //                    {
    //                        var storage = await StorageMedia.CreateAsync(file, false);
    //                        if (storage != null)
    //                        {
    //                            var array = this.ToArray();
    //                            var index = Array.BinarySearch(array, storage, _comparer);
    //                            if (index < 0) index = ~index;

    //                            // Insert only if newer than the last item
    //                            if (index < array.Length || !HasMoreItems)
    //                            {
    //                                _startIndex++;

    //                                Insert(index, storage);
    //                                storage.PropertyChanged += OnPropertyChanged;
    //                            }
    //                        }
    //                    });
    //                }
    //                break;
    //            // File Removed From Library
    //            case StorageLibraryChangeType.Deleted:
    //            case StorageLibraryChangeType.MovedOutOfLibrary:
    //                if (Constants.MediaTypes.Any(x => change.Path.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
    //                {
    //                    Execute.BeginOnUIThread(() =>
    //                    {
    //                        var already = this.FirstOrDefault(x => x.File.Path.Equals(change.Path));
    //                        if (already != null)
    //                        {
    //                            _startIndex--;

    //                            Remove(already);
    //                            UpdateSelected();
    //                        }
    //                    });
    //                }
    //                break;
    //            // Modified Contents
    //            case StorageLibraryChangeType.ContentsChanged:
    //                if (Constants.MediaTypes.Any(x => change.Path.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
    //                {
    //                    var file = (StorageFile)(await change.GetStorageItemAsync());

    //                    // Update thumbnail maybe
    //                }
    //                break;
    //            // Ignored Cases
    //            case StorageLibraryChangeType.EncryptionChanged:
    //            case StorageLibraryChangeType.ContentsReplaced:
    //            case StorageLibraryChangeType.IndexingStatusChanged:
    //            default:
    //                // These are safe to ignore in this application
    //                break;
    //        }
    //    }

    //    public override async Task<IList<StorageMedia>> LoadDataAsync()
    //    {
    //        if (_library == null)
    //        {
    //            _library = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
    //            _library.ChangeTracker.Enable();

    //            var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, Constants.MediaTypes);
    //            queryOptions.FolderDepth = FolderDepth.Deep;

    //            _query = KnownFolders.PicturesLibrary.CreateFileQueryWithOptions(queryOptions);
    //            _query.ContentsChanged += OnContentsChanged;
    //        }

    //        var items = new List<StorageMedia>();
    //        var result = await _query.GetFilesAsync(_startIndex, 10);

    //        _startIndex += (uint)result.Count;

    //        foreach (var file in result)
    //        {
    //            var storage = await StorageMedia.CreateAsync(file, false);
    //            if (storage != null)
    //            {
    //                items.Add(storage);
    //                storage.PropertyChanged += OnPropertyChanged;
    //            }
    //        }

    //        return items;
    //    }

    //    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    //    {
    //        if (e.PropertyName.Equals("IsSelected"))
    //        {
    //            UpdateSelected();
    //        }
    //    }

    //    private void UpdateSelected()
    //    {
    //        _selectedCount = this.Count(x => x.IsSelected);
    //        OnPropertyChanged(new PropertyChangedEventArgs("SelectedCount"));
    //    }

    //    class StorageMediaComparer : IComparer<StorageMedia>
    //    {
    //        public int Compare(StorageMedia x, StorageMedia y)
    //        {
    //            return y.Basic.ItemDate.CompareTo(x.Basic.ItemDate);
    //        }
    //    }
    //}
}
