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

namespace Unigram.Views
{
    public sealed partial class DialogPage : Page
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public BindConvert Convert => BindConvert.Current;

        public DialogPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<DialogViewModel>();

            CheckMessageBoxEmpty();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            ViewModel.PropertyChanged += OnPropertyChanged;

            lvDialogs.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

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
            //    ElementCompositionPreview.SetImplicitShowAnimation(btnDialogInfo, hideShowAnimation);
            //    ElementCompositionPreview.SetImplicitHideAnimation(btnDialogInfo, showHideAnimation);
            //}
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

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("Reply"))
            {
                CheckMessageBoxEmpty();
            }
        }

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (lvDialogs.SelectionMode == ListViewSelectionMode.None)
            {
                ManagePanel.Visibility = Visibility.Collapsed;
                btnDialogInfo.Visibility = Visibility.Visible;
            }
            else
            {
                ManagePanel.Visibility = Visibility.Visible;
                btnDialogInfo.Visibility = Visibility.Collapsed;
            }

            ViewModel.MessagesForwardCommand.RaiseCanExecuteChanged();
            ViewModel.MessagesDeleteCommand.RaiseCanExecuteChanged();
        }

        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (lvDialogs.SelectionMode == ListViewSelectionMode.None)
            {
                lvDialogs.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                lvDialogs.SelectionMode = ListViewSelectionMode.None;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

            _panel = (ItemsStackPanel)lvDialogs.ItemsPanelRoot;
            lvDialogs.ScrollingHost.ViewChanged += OnViewChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
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

        //private bool _isAlreadyLoading;
        //private bool _isAlreadyCalled;

        private void CheckMessageBoxEmpty()
        {
            var forwarding = false;
            if (ViewModel.Reply is TLMessagesContainter container)
            {
                forwarding = container.FwdMessages != null && container.FwdMessages.Count > 0;
            }

            if (ViewModel != null && txtMessage.IsEmpty && !forwarding)
            {
                btnSendMessage.Visibility = Visibility.Collapsed;
                btnStickers.Visibility = Visibility.Visible;
                btnVoiceMessage.Visibility = Visibility.Visible;
            }
            else
            {
                btnStickers.Visibility = Visibility.Collapsed;
                btnVoiceMessage.Visibility = Visibility.Collapsed;
                btnSendMessage.Visibility = Visibility.Visible;
            }
        }

        private void txtMessage_TextChanging(RichEditBox sender, RichEditBoxTextChangingEventArgs args)
        {
            CheckMessageBoxEmpty();
        }

        private async void btnSendMessage_Click(object sender, RoutedEventArgs e)
        {
            await txtMessage.SendAsync();
        }

        private void btnDialogInfo_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.With is TLUserBase)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), ViewModel.Peer.ToPeer());
            }
            else if (ViewModel.With is TLChannel)
            {
                ViewModel.NavigationService.Navigate(typeof(ChatDetailsPage), ViewModel.Peer.ToPeer());
            }
            else if (ViewModel.With is TLChat)
            {
                ViewModel.NavigationService.Navigate(typeof(ChatDetailsPage), ViewModel.Peer.ToPeer());
            }
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            var flyout = FlyoutBase.GetAttachedFlyout(Attach) as MenuFlyout;
            if (flyout != null)
            {
                var bounds = ApplicationView.GetForCurrentView().VisibleBounds;
                if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile" && (bounds.Width < 500 || bounds.Height < 500))
                {
                    flyout.LightDismissOverlayMode = LightDismissOverlayMode.On;
                }
                else
                {
                    flyout.LightDismissOverlayMode = LightDismissOverlayMode.Auto;
                }

                flyout.ShowAt(Attach, new Point(8, -8));
            }
        }

        private void SendShrug() => txtMessage.InsertText("¯\\_(ツ)_/¯");

        private void AttachPickerFlyout_ItemClick(object sender, MediaSelectedEventArgs e)
        {
            var flyout = FlyoutBase.GetAttachedFlyout(Attach) as MenuFlyout;
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
            //    if (txtMessage.Text == "")
            //    {
            //        txtMessage.Text = link.AbsolutePath;
            //    }
            //    else
            //    {
            //        txtMessage.Text = (txtMessage.Text + " " + link.AbsolutePath);
            //    }
            //
            //    gridLoading.Visibility = Visibility.Collapsed;
            //
            //}
            else if (e.DataView.Contains(StandardDataFormats.Text))
            {
                var text = await e.DataView.GetTextAsync();

                if (txtMessage.Text == "")
                {
                    txtMessage.Text = text;
                }
                else
                {
                    txtMessage.Text = (txtMessage.Text + " " + text);
                }

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

        private void Stickers_Click(object sender, RoutedEventArgs e)
        {
            StickersPanel.Visibility = StickersPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

            if (StickersPanel.Visibility == Visibility.Visible)
            {
                ViewModel.OpenStickersCommand.Execute(null);
                InputPane.GetForCurrentView().TryHide();
            }
            else
            {
                InputPane.GetForCurrentView().TryShow();
            }
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
            ViewModel.SelectedMessages = new List<TLMessageBase>(lvDialogs.SelectedItems.Cast<TLMessageBase>());
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

                    var channel = ViewModel.With as TLChannel;
                    if (channel != null)
                    {
                        if (channel.IsBroadcast)
                        {
                            element.Visibility = channel.IsCreator || channel.IsEditor ? Visibility.Visible : Visibility.Collapsed;
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
                    var channel = ViewModel.With as TLChannel;
                    if (channel != null && (channel.IsEditor || channel.IsCreator) && !channel.IsBroadcast)
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
                    var channel = ViewModel.With as TLChannel;
                    if (message.HasFwdFrom == false && message.ViaBotId == null && (message.IsOut || (channel != null && channel.IsBroadcast && (channel.IsCreator || channel.IsEditor))) && (message.Media is ITLMessageMediaCaption || message.Media is TLMessageMediaWebPage || message.Media is TLMessageMediaEmpty || message.Media == null))
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
                    var channel = ViewModel.With as TLChannel;
                    if (channel != null)
                    {
                        if (messageCommon.Id == 1 && messageCommon.ToId is TLPeerChannel)
                        {
                            element.Visibility = Visibility.Collapsed;
                        }

                        if (!messageCommon.IsOut && !channel.IsCreator && !channel.IsEditor)
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
                    var channel = ViewModel.With as TLChannel;
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

        private void Stickers_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.SendStickerCommand.Execute(e.ClickedItem);
            ViewModel.StickerPack = null;
            txtMessage.Text = null;
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
    }

    public class MediaLibraryCollection : IncrementalCollection<StoragePhoto>, ISupportIncrementalLoading
    {
        public StorageFileQueryResult Query { get; private set; }

        public uint StartIndex { get; private set; }

        private CoreDispatcher _dispatcher;

        public MediaLibraryCollection()
        {
            if (Windows.ApplicationModel.DesignMode.DesignModeEnabled) return;

            var queryOptions = new QueryOptions(CommonFileQuery.OrderByDate, Constants.MediaTypes);
            queryOptions.FolderDepth = FolderDepth.Deep;

            Query = KnownFolders.PicturesLibrary.CreateFileQueryWithOptions(queryOptions);
            Query.ContentsChanged += OnContentsChanged;
            StartIndex = 0;

            _dispatcher = Window.Current.Dispatcher;
        }

        private async void OnContentsChanged(IStorageQueryResultBase sender, object args)
        {
            await _dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StartIndex = 0;
                Clear();
            });
        }

        public override async Task<IEnumerable<StoragePhoto>> LoadDataAsync()
        {
            var items = new List<StoragePhoto>();
            uint resultCount = 0;
            var result = await Query.GetFilesAsync(StartIndex, 10);
            StartIndex += (uint)result.Count;

            resultCount = (uint)result.Count;

            foreach (var file in result)
            {
                items.Add(new StoragePhoto(file));
            }

            return items;
        }
    }
}
