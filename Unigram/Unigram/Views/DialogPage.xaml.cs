﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Dependency;
using Unigram.Core.Models;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
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

            DataContext = UnigramContainer.Instance.ResolveType<DialogViewModel>();
            CheckMessageBoxEmpty();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

            _panel = (ItemsStackPanel)lvDialogs.ItemsPanelRoot;
            lvDialogs.ScrollingHost.ViewChanged += OnViewChanged;

            lvDialogs.ScrollingHost.ViewChanged += LvScroller_ViewChanged;
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
            ReplyMarkupViewer.MaxHeight = args.OccludedRect.Height;
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            KeyboardPlaceholder.Height = new GridLength(1, GridUnitType.Auto);
        }

        //private bool _isAlreadyLoading;
        //private bool _isAlreadyCalled;

        private async void LvScroller_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (lvDialogs.ScrollingHost.VerticalOffset < 120 && !e.IsIntermediate)
            {
                await ViewModel.LoadNextSliceAsync();
            }
        }

        private void CheckMessageBoxEmpty()
        {
            if (txtMessage.IsEmpty)
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
            if (ViewModel.With is TLUserBase) //Se non è zuppa allora è pan bagnato
                ViewModel.NavigationService.Navigate(typeof(UserInfoPage), ViewModel.Peer.ToPeer());
            else if (ViewModel.With is TLChannel)
                ViewModel.NavigationService.Navigate(typeof(ChatInfoPage), ViewModel.Peer);
            else if (ViewModel.With is TLChat)
                ViewModel.NavigationService.Navigate(typeof(ChatInfoPage), ViewModel.Peer);
        }

        private void btnClosePinnedMessage_Click(object sender, RoutedEventArgs e)
        {
            grdPinnedMessage.Visibility = Visibility.Collapsed;
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

        private void ReplyMarkup_ButtonClick(object sender, ReplyMarkupButtonClickEventArgs e)
        {
            ViewModel.KeyboardButtonExecute(e.Button, null);
        }

        #region Context menu

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
                }
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
                    if (channel != null && (channel.IsEditor || channel.IsCreator))
                    {
                        if (messageCommon.ToId is TLPeerChannel)
                        {
                            element.Visibility = Visibility.Visible;
                            element.Text = ViewModel.PinnedMessage != null && ViewModel.PinnedMessage.Id == messageCommon.Id ? "Unpin" : "Pin";
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
                    if (message.HasFwdFrom == false && message.ViaBotId == null && (message.IsOut || (channel != null && channel.IsCreator && channel.IsEditor)) && (message.Media is ITLMediaCaption || message.Media is TLMessageMediaWebPage || message.Media is TLMessageMediaEmpty || message.Media == null))
                    {
                        if (message.IsSticker())
                        {
                            element.Visibility = Visibility.Collapsed;
                            return;
                        }

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

                    var mediaCaption = message.Media as ITLMediaCaption;
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

        #endregion
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
