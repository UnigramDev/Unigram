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

namespace Unigram.Views
{
    public sealed partial class DialogPage : Page, IMasterDetailPage
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public BindConvert Convert => BindConvert.Current;

        private DispatcherTimer _elapsedTimer;
        private Visual _messageVisual;
        private Visual _ellipseVisual;
        private Visual _elapsedVisual;
        private Visual _slideVisual;
        private Visual _rootVisual;
        private Compositor _compositor;

        public DialogPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<DialogViewModel>();

            //NavigationCacheMode = NavigationCacheMode.Required;

            ViewModel.TextField = TextField;
            ViewModel.ListField = lvDialogs;

            CheckMessageBoxEmpty();

            ViewModel.PropertyChanged += OnPropertyChanged;

            TextField.LostFocus += TextField_LostFocus;

            lvDialogs.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

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
                StickersPanel.Visibility = Visibility.Collapsed;

                if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                {
                    TextField.Focus(FocusState.Keyboard);
                }
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
            ViewModel.IsActive = true;

            base.OnNavigatedTo(e);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            //if (_panel != null && ViewModel.With != null)
            //{
            //    var container = lvDialogs.ContainerFromIndex(_panel.FirstVisibleIndex);
            //    if (container != null)
            //    {
            //        var peer = ViewModel.With.ToPeer();
            //        var item = lvDialogs.ItemFromContainer(container) as TLMessageBase;

            //        ApplicationSettings.Current.AddOrUpdateValue(TLSerializationService.Current.Serialize(peer), item?.Id ?? -1);
            //    }
            //}

            Bindings.StopTracking();

            base.OnNavigatingFrom(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode != NavigationMode.Forward || e.SourcePageType != typeof(DialogPage) || e.Parameter != Frame.BackStack.Last()?.Parameter)
            {
                ViewModel.IsActive = false;
            }

            base.OnNavigatedFrom(e);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Reply"))
            {
                CheckMessageBoxEmpty();
            }
            else if (e.PropertyName.Equals("SelectedItems"))
            {
                lvDialogs.SelectedItems.AddRange(ViewModel.SelectedMessages);
            }
        }

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (ViewModel.SelectionMode == ListViewSelectionMode.None)
            {
                ManagePanel.Visibility = Visibility.Collapsed;
                InfoPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ManagePanel.Visibility = Visibility.Visible;
                InfoPanel.Visibility = Visibility.Collapsed;
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

            App.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            _panel = (ItemsStackPanel)lvDialogs.ItemsPanelRoot;
            lvDialogs.ScrollingHost.ViewChanged += OnViewChanged;

            if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                TextField.Focus(FocusState.Keyboard);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
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
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            KeyboardPlaceholder.Height = new GridLength(1, GridUnitType.Auto);
        }

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Escape && !args.KeyStatus.IsKeyReleased && ViewModel.SelectionMode != ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
                args.Handled = true;
            }
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            if (StickersPanel.Visibility == Visibility.Visible)
            {
                StickersPanel.Visibility = Visibility.Collapsed;
                args.Handled = true;
            }

            if (ViewModel.SelectionMode != ListViewSelectionMode.None)
            {
                ViewModel.SelectionMode = ListViewSelectionMode.None;
                args.Handled = true;
            }

            if (args.Handled)
            {
                Focus(FocusState.Programmatic);

                if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                {
                    TextField.Focus(FocusState.Keyboard);
                }
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
                btnStickers.Visibility = Visibility.Visible;
                btnVoiceMessage.Visibility = Visibility.Visible;
            }
            else
            {
                btnSendMessage.Visibility = Visibility.Visible;
                btnCommands.Visibility = Visibility.Collapsed;
                btnStickers.Visibility = Visibility.Collapsed;
                btnVoiceMessage.Visibility = Visibility.Collapsed;
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
            if (ViewModel.With is TLUserBase)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), ViewModel.Peer.ToPeer());
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
            var channel = ViewModel.With as TLChannel;
            if (channel != null && channel.HasBannedRights && channel.BannedRights.IsSendMedia)
            {
                await TLMessageDialog.ShowAsync("The admins of this group restricted you from posting media content here.", "Warning", "OK");
                return;
            }

            var pane = InputPane.GetForCurrentView();
            if (pane.OccludedRect != Rect.Empty)
            {
                pane.TryHide();

                // TODO: Can't find any better solution
                await Task.Delay(200);
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

                flyout.ShowAt(ButtonAttach, new Point(8, -8));
            }
        }

        private void SendShrug() => TextField.InsertText("¯\\_(ツ)_/¯");

        private void AttachPickerFlyout_ItemClick(object sender, MediaSelectedEventArgs e)
        {
            var flyout = FlyoutBase.GetAttachedFlyout(ButtonAttach) as MenuFlyout;
            if (flyout != null)
            {
                flyout.Hide();
            }

            ViewModel.SendPhotoCommand.Execute(e.Item.Clone());
        }

        private void InlineBotResults_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SendBotInlineResult((TLBotInlineResultBase)e.ClickedItem);
        }

        private void gridMain_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void gridMain_Drop(object sender, DragEventArgs e)
        {
            //gridLoading.Visibility = Visibility.Visible;

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                ObservableCollection<StorageFile> images = new ObservableCollection<StorageFile>();
                ObservableCollection<StorageFile> audio = new ObservableCollection<StorageFile>();
                ObservableCollection<StorageFile> videos = new ObservableCollection<StorageFile>();
                ObservableCollection<StorageFile> files = new ObservableCollection<StorageFile>();


                // Check for file types and sort these in the correct Collections
                foreach (StorageFile file in items)
                {
                    // Which of the two is better practise? The second one seems more foolproof imho    - Rick

                    //if (file.FileType == ".jpg" || file.ContentType == ".png")
                    //{
                    //    images.Add(file);
                    //}

                    // Images first
                    if (file.ContentType == "image/jpeg" || file.ContentType == "image/png")
                    {
                        images.Add(file);
                    }
                    // Audio second
                    else if (file.ContentType == "audio/mpeg" || file.ContentType == "audio/x-wav")
                    {
                        audio.Add(file);
                    }
                    // Videos third
                    else if (file.ContentType == "video/mpeg" || file.ContentType == "video/mp4")
                    {
                        videos.Add(file);
                    }
                    // files last
                    else
                    {
                        files.Add(file);
                    }


                }
                // Send images
                if (images.Count > 0)
                {
                    //gridLoading.Visibility = Visibility.Collapsed;
                    ViewModel.SendPhotoDrop(images);
                }
                //if (audio.Count > 0)
                //{
                //    gridLoading.Visibility = Visibility.Collapsed;
                //}
                //if (videos.Count > 0)
                //{
                //    gridLoading.Visibility = Visibility.Collapsed;
                //}
                //if (files.Count > 0)
                //{
                //    gridLoading.Visibility = Visibility.Collapsed;
                //}
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
            else if (e.DataView.Contains(StandardDataFormats.Text))
            {
                var text = await e.DataView.GetTextAsync();
                TextField.Document.GetRange(TextField.Document.Selection.EndPosition, TextField.Document.Selection.EndPosition).SetText(TextSetOptions.None, text);

                //gridLoading.Visibility = Visibility.Collapsed;
            }




        }

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
            ViewModel.KeyboardButtonExecute(e.Button, null);
        }

        private async void Stickers_Click(object sender, RoutedEventArgs e)
        {
            var channel = ViewModel.With as TLChannel;
            if (channel != null && channel.HasBannedRights && (channel.BannedRights.IsSendStickers || channel.BannedRights.IsSendGifs))
            {
                await TLMessageDialog.ShowAsync("The admins of this group restricted you from posting stickers here.", "Warning", "OK");
                return;
            }

            if (StickersPanel.Visibility == Visibility.Collapsed)
            {
                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Programmatic);

                InputPane.GetForCurrentView().TryHide();

                StickersPanel.Visibility = Visibility.Visible;

                ViewModel.OpenStickersCommand.Execute(null);
            }
            else
            {
                Focus(FocusState.Programmatic);
                TextField.Focus(FocusState.Keyboard);

                StickersPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void Commands_Click(object sender, RoutedEventArgs e)
        {
            TextField.SetText("/", null);
            TextField.Focus(FocusState.Keyboard);
        }

        private void ProfileBubble_Click(object sender, RoutedEventArgs e)
        {
            var control = sender as FrameworkElement;
            var message = control.DataContext as TLMessage;
            if (message != null && message.HasFromId)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), new TLPeerUser { UserId = message.FromId.Value });
            }
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectedMessages = new List<TLMessageCommonBase>(lvDialogs.SelectedItems.Cast<TLMessageCommonBase>());
        }

        #region Context menu

        private void MenuFlyout_Opening(object sender, object e)
        {
            var flyout = sender as MenuFlyout;

            foreach (var item in flyout.Items)
            {
                item.Visibility = Visibility.Visible;
            }
        }

        private void MessageReply_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var messageCommon = element.DataContext as TLMessageCommonBase;
                if (messageCommon != null)
                {
                    //var channel = ViewModel.With as TLChannel;
                    //if (channel != null && channel.MigratedFromChatId != null)
                    //{
                    //    if (messageCommon.ToId is TLPeerChat)
                    //    {
                    //        element.Visibility = messageCommon.ToId.Id == channel.MigratedFromChatId ? Visibility.Collapsed : Visibility.Visible;
                    //    }
                    //}

                    var channel = messageCommon.Parent as TLChannel;
                    if (channel != null)
                    {
                        if (channel.IsBroadcast)
                        {
                            element.Visibility = channel.IsCreator || channel.HasAdminRights ? Visibility.Visible : Visibility.Collapsed;
                            return;
                        }
                    }
                }

                element.Visibility = Visibility.Visible;
            }
        }

        private void MessagePin_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var messageCommon = element.DataContext as TLMessageCommonBase;
                if (messageCommon != null)
                {
                    var channel = messageCommon.Parent as TLChannel;
                    if (channel != null && (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsPinMessages)) && !channel.IsBroadcast)
                    {
                        if (messageCommon.ToId is TLPeerChannel)
                        {
                            element.Visibility = Visibility.Visible;
                            element.Text = ViewModel.PinnedMessage != null && ViewModel.PinnedMessage.Id == messageCommon.Id ? "Unpin message" : "Pin message";
                            return;
                        }
                    }
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        private void MessageEdit_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var message = element.DataContext as TLMessage;
                if (message != null)
                {
                    var channel = message.Parent as TLChannel;
                    if (message.IsOut && message.ToId is TLPeerUser userPeer && userPeer.Id == SettingsHelper.UserId)
                    {
                        element.Visibility = Visibility.Visible;
                        return;
                    }
                    else if (message.HasFwdFrom == false && message.ViaBotId == null && (message.IsOut || (channel != null && channel.IsBroadcast && (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsEditMessages)))) && (message.Media is ITLMessageMediaCaption || message.Media is TLMessageMediaWebPage || message.Media is TLMessageMediaEmpty || message.Media == null))
                    {
                        var date = TLUtils.DateToUniversalTimeTLInt(ViewModel.ProtoService.ClientTicksDelta, DateTime.Now);
                        var config = ViewModel.CacheService.GetConfig();
                        if (config != null && message.Date + config.EditTimeLimit < date)
                        {
                            element.Visibility = Visibility.Collapsed;
                            return;
                        }

                        element.Visibility = Visibility.Visible;
                        return;
                    }
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        private void MessageDelete_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                element.Visibility = Visibility.Visible;

                var messageCommon = element.DataContext as TLMessageCommonBase;
                if (messageCommon != null)
                {
                    var channel = messageCommon.Parent as TLChannel;
                    if (channel != null)
                    {
                        if (messageCommon.Id == 1 && messageCommon.ToId is TLPeerChannel)
                        {
                            element.Visibility = Visibility.Collapsed;
                        }

                        if (!messageCommon.IsOut && !channel.IsCreator && !channel.HasAdminRights || (channel.AdminRights != null && !channel.AdminRights.IsDeleteMessages))
                        {
                            element.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        private void MessageForward_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var messageCommon = element.DataContext as TLMessageCommonBase;
                if (messageCommon != null)
                {

                }

                element.Visibility = Visibility.Visible;
            }
        }

        private void MessageCopy_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var message = element.DataContext as TLMessage;
                if (message != null)
                {
                    if (!string.IsNullOrEmpty(message.Message))
                    {
                        Visibility = Visibility.Visible;
                        return;
                    }

                    var mediaCaption = message.Media as ITLMessageMediaCaption;
                    if (mediaCaption != null && !string.IsNullOrEmpty(mediaCaption.Caption))
                    {
                        element.Visibility = Visibility.Visible;
                        return;
                    }
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        private void MessageCopyLink_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var messageCommon = element.DataContext as TLMessageCommonBase;
                if (messageCommon != null)
                {
                    var channel = messageCommon.Parent as TLChannel;
                    if (channel != null)
                    {
                        if (channel.IsBroadcast && channel.HasUsername)
                        {
                            element.Visibility = Visibility.Visible;
                            return;
                        }
                    }
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        private void MessageSelect_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                element.Visibility = ViewModel.SelectionMode == ListViewSelectionMode.None ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void MessageStickerPackInfo_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void MessageSaveSticker_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void MessageSaveMedia_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var message = element.DataContext as TLMessage;
                if (message != null)
                {
                    if (message.Media is TLMessageMediaDocument || message.Media is TLMessageMediaPhoto)
                    {
                        Visibility = Visibility.Visible;
                        return;
                    }
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        private void MessageSaveGIF_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var message = element.DataContext as TLMessage;
                if (message != null)
                {
                    if (message.IsGif())
                    {
                        Visibility = Visibility.Visible;
                        return;
                    }
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        private void MessageCallAgain_Loaded(object sender, RoutedEventArgs e)
        {
            var element = sender as MenuFlyoutItem;
            if (element != null)
            {
                var message = element.DataContext as TLMessageService;
                if (message != null)
                {
                    if (message.Action is TLMessageActionPhoneCall)
                    {
                        Visibility = Visibility.Visible;
                        return;
                    }
                }

                element.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        private void Download_Click(object sender, TransferCompletedEventArgs e)
        {
            Media.Download(sender, e);
        }

        private async void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            var channel = ViewModel.With as TLChannel;
            if (channel != null && channel.HasBannedRights && (channel.BannedRights.IsSendStickers || channel.BannedRights.IsSendGifs))
            {
                await TLMessageDialog.ShowAsync("The admins of this group restricted you from posting stickers here.", "Warning", "OK");
                return;
            }

            ViewModel.SendStickerCommand.Execute(e.ClickedItem);
            ViewModel.StickerPack = null;
            TextField.SetText(null, null);
        }

        private async void StickerSet_Click(object sender, RoutedEventArgs e)
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

        private void BotCommands_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as Tuple<TLUser, TLBotCommand>;
            if (item != null)
            {
                var command = $"/{item.Item2.Command}";
                if (ViewModel.With is TLChannel || ViewModel.With is TLChat && item.Item1.HasUsername)
                {
                    command += $"@{item.Item1.Username}";
                }

                TextField.SetText(null, null);
                ViewModel.SendCommand.Execute(command);
                ViewModel.BotCommands = null;
            }
        }

        private void UsernameHints_ItemClick(object sender, ItemClickEventArgs e)
        {
            TextField.Document.GetText(TextGetOptions.None, out string hidden);
            TextField.Document.GetText(TextGetOptions.NoHidden, out string text);

            var user = e.ClickedItem as TLUser;
            if (user != null && BubbleTextBox.SearchByUsernames(text.Substring(0, Math.Min(TextField.Document.Selection.EndPosition, text.Length)), out string query))
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
                var start = TextField.Document.Selection.StartPosition - query.Length - adjust + insert.Length;
                var range = TextField.Document.GetRange(TextField.Document.Selection.StartPosition - query.Length - adjust, TextField.Document.Selection.StartPosition);
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

                ViewModel.UsernameHints = null;
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
            return userId != 777000 && userId != 429000 && userId != 4244000 && (userId / 1000 == 333 || userId % 1000 == 0) ? "Got a question about Telegram?" : "No messages here yet...";
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
    }

    public class MediaLibraryCollection : IncrementalCollection<StorageMedia>, ISupportIncrementalLoading
    {
        public StorageFileQueryResult Query { get; private set; }
        public uint StartIndex { get; private set; }

        public MediaLibraryCollection()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled) return;

            var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, Constants.MediaTypes);
            queryOptions.FolderDepth = FolderDepth.Deep;

            Query = KnownFolders.PicturesLibrary.CreateFileQueryWithOptions(queryOptions);
            Query.ContentsChanged += OnContentsChanged;
            StartIndex = 0;
        }

        private void OnContentsChanged(IStorageQueryResultBase sender, object args)
        {
            Execute.BeginOnUIThread(() =>
            {
                StartIndex = 0;
                Clear();
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
                if (Path.GetExtension(file.Name).Equals(".mp4"))
                {
                    items.Add(new StorageVideo(file));
                }
                else
                {
                    items.Add(new StoragePhoto(file));
                }
            }

            return items;
        }
    }
}
