using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class InlineBotResultsView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public InlineBotResultsView()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _panel = Items.ItemsPanelRoot as ItemsStackPanel;

            var scroll = Items.ScrollingHost;
            if (scroll != null)
            {
                scroll.ViewChanged += OnViewChanged;
            }
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null) Bindings.Update();
            if (ViewModel == null) Bindings.StopTracking();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(sender, e);
        }

        public event ItemClickEventHandler ItemClick;

        private Visibility ConvertBannedRights(Chat chat, bool invert)
        {
            if (chat != null && chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && supergroup.Status is ChatMemberStatusRestricted restricted && !restricted.CanSendOtherMessages)
                {
                    return invert ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            return invert ? Visibility.Visible : Visibility.Collapsed;
        }

        private string ConvertBannedRights(Chat chat)
        {
            if (chat != null && chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup != null && supergroup.Status is ChatMemberStatusRestricted restricted && !restricted.CanSendOtherMessages)
                {
                    if (restricted.IsForever())
                    {
                        return Strings.Resources.AttachInlineRestrictedForever;
                    }
                    else
                    {
                        return string.Format(Strings.Resources.AttachInlineRestricted, BindConvert.Current.BannedUntil(restricted.RestrictedUntilDate));
                    }
                }
            }

            return null;
        }

        #region Gifs

        private ItemsStackPanel _panel;

        private void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            //var index0 = _panel.FirstVisibleIndex;
            //var index1 = _panel.LastVisibleIndex;

            //if (index0 > -1 && index1 > -1 /*&& (index0 != _lastIndex0 || index1 != _lastIndex1)*/ && !e.IsIntermediate)
            //{
            //    var messages = new List<TLBotInlineResultBase>(index1 - index0);
            //    var auto = true;
            //    var news = new Dictionary<string, MediaPlayerItem>();

            //    for (int i = index0; i <= index1; i++)
            //    {
            //        var container = Items.ContainerFromIndex(i) as GridViewItem;
            //        if (container != null)
            //        {
            //            var item = Items.ItemFromContainer(container) as TLBotInlineResultBase;
            //            if (item == null)
            //            {
            //                continue;
            //            }

            //            messages.Add(item);
            //        }
            //    }

            //    Play(messages, auto);
            //}
        }

        private Dictionary<string, MediaPlayerItem> _old = new Dictionary<string, MediaPlayerItem>();

        class MediaPlayerItem
        {
            public Grid Container { get; set; }
            public MediaPlayerView Presenter { get; set; }
            public bool Watermark { get; set; }
        }

        //public void Play(IEnumerable<TLBotInlineResultBase> items, bool auto)
        //{
        //}

        #endregion

        public void UpdateFile(File file)
        {
            if (!file.Local.IsDownloadingCompleted)
            {
                return;
            }

            foreach (MosaicMediaRow line in Items.Items)
            {
                var any = false;
                foreach (var item in line)
                {
                    if (item.Item is InlineQueryResultAnimation animation && animation.Animation.UpdateFile(file))
                    {
                        any = true;
                        break;
                    }
                    else if (item.Item is InlineQueryResultPhoto photo && photo.Photo.UpdateFile(file))
                    {
                        any = true;
                        break;
                    }
                }

                if (!any)
                {
                    continue;
                }

                var container = Items.ContainerFromItem(line) as SelectorItem;
                if (container == null)
                {
                    continue;
                }

                var content = container.ContentTemplateRoot as MosaicRow;
                if (content == null)
                {
                    continue;
                }

                content.UpdateFile(line, file);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var position = args.Item as MosaicMediaRow;

            if (args.ItemContainer.ContentTemplateRoot is MosaicRow row)
            {
                row.UpdateLine(ViewModel.ProtoService, position, Item_Click);
            }
            else if (args.ItemContainer.ContentTemplateRoot is Button button)
            {
                var content = button.Content as Grid;
                var result = position[0].Item;

                button.Tag = result;

                var presenter = content.Children[0] as Grid;

                var title = content.Children[1] as TextBlock;
                var subtitle = content.Children[2] as TextBlock;

                if (result is InlineQueryResultArticle article)
                {
                    if (article.Thumbnail != null)
                    {
                        presenter.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        presenter.Visibility = Visibility.Collapsed;
                    }

                    title.Text = article.Title;
                    subtitle.Text = article.Description;
                }
                else if (result is InlineQueryResultContact contact)
                {
                    var user = ViewModel.ProtoService.GetUser(contact.Contact.UserId);
                    if (user != null)
                    {
                        title.Text = user.GetFullName();
                    }
                    else
                    {
                        title.Text = string.IsNullOrEmpty(contact.Contact.LastName) ? contact.Contact.FirstName : $"{contact.Contact.FirstName} {contact.Contact.LastName}";
                    }

                    subtitle.Text = PhoneNumber.Format(contact.Contact.PhoneNumber);
                }
                else if (result is InlineQueryResultGame game)
                {
                    presenter.Visibility = Visibility.Visible;

                    title.Text = game.Game.Title;
                    subtitle.Text = game.Game.Description;
                }
                else if (result is InlineQueryResultPhoto photo)
                {
                    presenter.Visibility = Visibility.Visible;

                    title.Text = photo.Title;
                    subtitle.Text = photo.Description;
                }
                else if (result is InlineQueryResultVideo video)
                {
                    presenter.Visibility = Visibility.Visible;

                    title.Text = video.Title;
                    subtitle.Text = video.Description;
                }
                else if (result is InlineQueryResultAudio audio)
                {
                    title.Text = audio.Audio.GetTitle();
                    subtitle.Text = "???";
                }
                else if (result is InlineQueryResultDocument document)
                {
                    if (document.Document.Thumbnail != null)
                    {
                        presenter.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        presenter.Visibility = Visibility.Collapsed;
                    }

                    title.Text = document.Title;
                    subtitle.Text = document.Description;
                }
                else if (result is InlineQueryResultVoiceNote voiceNote)
                {

                }

                if (result is InlineQueryResultArticle || result is InlineQueryResultContact || result is InlineQueryResultGame || result is InlineQueryResultPhoto || result is InlineQueryResultVideo)
                {
                    //var photo = content.Children[0] as ProfilePicture;

                    //var file = user.ProfilePhoto?.Small;
                    //if (file != null)
                    //{
                    //    if (file.Local.IsDownloadingCompleted)
                    //    {
                    //        photo.Source = new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = 36, DecodePixelHeight = 36, DecodePixelType = DecodePixelType.Logical };
                    //    }
                    //    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    //    {
                    //        ViewModel.ProtoService.Send(new DownloadFile(file.Id, 1));
                    //    }
                    //}
                    //else
                    //{
                    //    photo.Source = null;
                    //}
                }
                else if (result is InlineQueryResultAudio || result is InlineQueryResultDocument)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Break();
                }
            }

            args.Handled = true;
        }

        private void Item_Click(object item)
        {
            var collection = ViewModel.InlineBotResults;
            if (collection == null)
            {
                return;
            }

            var result = item as InlineQueryResult;
            if (result == null)
            {
                return;
            }

            ViewModel.SendBotInlineResult(result, collection.GetQueryId(result));
        }

        private void OnContainerContentChanging2(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var result = args.Item as InlineQueryResult;

            if (result.IsMedia())
            {
                var texture = content.Children[0] as Image;

                var panel = content as AspectView;
                if (panel == null)
                {
                    return;
                }

                panel.Constraint = result;

                if (result is InlineQueryResultAnimation animation && animation.Animation.Thumbnail != null)
                {
                    var file = animation.Animation.Thumbnail.Photo;
                    if (file.Local.IsDownloadingCompleted)
                    {
                        texture.Source = PlaceholderHelper.GetBlurred(file.Local.Path);
                    }
                    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        ViewModel.ProtoService.Send(new DownloadFile(file.Id, 1));
                    }
                }
                else if (result is InlineQueryResultLocation locationResult)
                {

                }
                else if (result is InlineQueryResultPhoto photoResult)
                {

                }
                else if (result is InlineQueryResultSticker stickerResult)
                {

                }
                else if (result is InlineQueryResultVideo videoResult)
                {

                }
            }
            else
            {
                var title = content.Children[1] as TextBlock;
                var subtitle = content.Children[2] as TextBlock;

                if (result is InlineQueryResultArticle article)
                {
                    title.Text = article.Title;
                    subtitle.Text = article.Description;
                }
                else if (result is InlineQueryResultContact contact)
                {
                    var user = ViewModel.ProtoService.GetUser(contact.Contact.UserId);
                    if (user != null)
                    {
                        title.Text = user.GetFullName();
                    }
                    else
                    {
                        title.Text = string.IsNullOrEmpty(contact.Contact.LastName) ? contact.Contact.FirstName : $"{contact.Contact.FirstName} {contact.Contact.LastName}";
                    }

                    subtitle.Text = PhoneNumber.Format(contact.Contact.PhoneNumber);
                }
                else if (result is InlineQueryResultGame game)
                {
                    title.Text = game.Game.Title;
                    subtitle.Text = game.Game.Description;
                }
                else if (result is InlineQueryResultPhoto photo)
                {
                    title.Text = photo.Title;
                    subtitle.Text = photo.Description;
                }
                else if (result is InlineQueryResultVideo video)
                {
                    title.Text = video.Title;
                    subtitle.Text = video.Description;
                }
                else if (result is InlineQueryResultAudio audio)
                {
                    title.Text = audio.Audio.GetTitle();
                    subtitle.Text = "???";
                }
                else if (result is InlineQueryResultDocument document)
                {
                    title.Text = document.Title;
                    subtitle.Text = document.Description;
                }
                else if (result is InlineQueryResultVoiceNote voiceNote)
                {

                }

                if (result is InlineQueryResultArticle || result is InlineQueryResultContact || result is InlineQueryResultGame || result is InlineQueryResultPhoto || result is InlineQueryResultVideo)
                {
                    //var photo = content.Children[0] as ProfilePicture;

                    //var file = user.ProfilePhoto?.Small;
                    //if (file != null)
                    //{
                    //    if (file.Local.IsDownloadingCompleted)
                    //    {
                    //        photo.Source = new BitmapImage(new Uri("file:///" + file.Local.Path)) { DecodePixelWidth = 36, DecodePixelHeight = 36, DecodePixelType = DecodePixelType.Logical };
                    //    }
                    //    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    //    {
                    //        ViewModel.ProtoService.Send(new DownloadFile(file.Id, 1));
                    //    }
                    //}
                    //else
                    //{
                    //    photo.Source = null;
                    //}
                }
                else if (result is InlineQueryResultAudio || result is InlineQueryResultDocument)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Break();
                }
            }

            //if (args.Phase < 2)
            //{
            //    args.RegisterUpdateCallback(OnContainerContentChanging);
            //}

            args.Handled = true;
        }

        private void Result_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var result = button.Tag as InlineQueryResult;

            Item_Click(result);
        }
    }
}
