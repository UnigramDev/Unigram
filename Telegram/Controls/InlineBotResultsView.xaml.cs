//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using System.Linq;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public sealed partial class InlineBotResultsView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        public InlineBotResultsView()
        {
            InitializeComponent();

            _handler = new AnimatedListHandler(ScrollingHost, AnimatedListType.Other);

            _zoomer = new ZoomableListHandler(ScrollingHost);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ClientService.SessionId;
        }

        public void UpdateCornerRadius(double radius)
        {
            var min = Math.Max(4, radius - 2);

            Root.Padding = new Thickness(0, 0, 0, radius);
            SwitchPm.CornerRadius = new CornerRadius(min, min, 4, 4);

            CornerRadius = new CornerRadius(radius, radius, 0, 0);
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null)
            {
                Bindings.Update();
            }

            if (ViewModel == null)
            {
                Bindings.StopTracking();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }

        public event ItemClickEventHandler ItemClick;

        public void UpdateChatPermissions(Chat chat)
        {
            var rights = ViewModel.VerifyRights(chat, x => x.CanSendOtherMessages, Strings.GlobalAttachInlineRestricted, Strings.AttachInlineRestrictedForever, Strings.AttachInlineRestricted, out string label);

            LayoutRoot.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
            PermissionsPanel.Visibility = rights ? Visibility.Visible : Visibility.Collapsed;
            PermissionsLabel.Text = label ?? string.Empty;
        }

        private void UpdateFile(object target, File file)
        {
            if (target is Grid content)
            {
                if (content.Children[0] is LottieView stickerView)
                {
                    stickerView.Source = UriEx.ToLocal(file.Local.Path);
                }
                else if (content.Children[0] is AnimationView animationView)
                {
                    animationView.Source = new LocalVideoSource(file);
                }
            }

            _handler.ThrottleVisibleItems();
        }

        private void UpdateThumbnail(object target, File file)
        {
            if (target is Image image)
            {
                if (image.Tag is InlineQueryResultSticker)
                {
                    image.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                }
                else
                {
                    image.Source = new BitmapImage(UriEx.ToLocal(file.Local.Path));
                }
            }
            else if (target is AnimationView animationView)
            {
                animationView.Thumbnail = new BitmapImage(UriEx.ToLocal(file.Local.Path));
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is InlineQueryResult result)
            {
                var collection = ViewModel.InlineBotResults;
                if (collection == null)
                {
                    return;
                }

                ViewModel.SendBotInlineResult(result, collection.GetQueryId(result));
            }
        }

        private object ConvertSource(BotResultsCollection collection)
        {
            if (collection == null)
            {
                return null;
            }
            else if (collection.All(x => x is InlineQueryResultSticker) || collection.All(x => x.IsMedia()))
            {
                if (ScrollingHost.ItemsPanel != VerticalGrid)
                {
                    ScrollingHost.ItemsPanel = VerticalGrid;
                    ScrollingHost.ItemTemplate = null;
                    ScrollingHost.ItemTemplateSelector = MediaTemplateSelector;

                    FluidGridView.Update(ScrollingHost);
                }
            }
            else if (ScrollingHost.ItemsPanel != VerticalStack)
            {
                ScrollingHost.ItemsPanel = VerticalStack;
                ScrollingHost.ItemTemplate = ResultTemplate;
                ScrollingHost.ItemTemplateSelector = null;
            }

            return new object();
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                if (sender.ItemsPanel == VerticalStack)
                {
                    args.ItemContainer = new ListViewItem();
                }
                else
                {
                    args.ItemContainer = new GridViewItem
                    {
                        Margin = new Thickness(2)
                    };
                }

                if (sender.ItemTemplateSelector != null)
                {
                    args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
                }
                else
                {
                    args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                }

                args.ItemContainer.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                args.ItemContainer.VerticalContentAlignment = VerticalAlignment.Stretch;

                _zoomer.ElementPrepared(args.ItemContainer);
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var result = args.Item as InlineQueryResult;

            if (content.Children[0] is Image image)
            {
                image.Tag = args.Item;

                if (result is InlineQueryResultPhoto or InlineQueryResultVideo)
                {
                    File file = null;
                    if (result is InlineQueryResultPhoto photo)
                    {
                        file = photo.Photo.GetSmall().Photo;
                    }
                    else if (result is InlineQueryResultVideo video)
                    {
                        file = video.Video.Thumbnail?.File;
                    }

                    if (file == null)
                    {
                        return;
                    }

                    if (file.Local.IsDownloadingCompleted)
                    {
                        image.Source = new BitmapImage(UriEx.ToLocal(file.Local.Path));
                    }
                    else
                    {
                        image.Source = null;
                        UpdateManager.Subscribe(image, ViewModel.ClientService, file, UpdateThumbnail, true);

                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            ViewModel.ClientService.DownloadFile(file.Id, 1);
                        }
                    }
                }
                else if (result is InlineQueryResultSticker sticker)
                {
                    var file = sticker.Sticker.StickerValue;
                    if (file == null)
                    {
                        return;
                    }

                    if (file.Local.IsDownloadingCompleted)
                    {
                        image.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    }
                    else
                    {
                        image.Source = null;
                        UpdateManager.Subscribe(image, ViewModel.ClientService, file, UpdateThumbnail, true);

                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            ViewModel.ClientService.DownloadFile(file.Id, 1);
                        }
                    }
                }
            }
            else if (result is InlineQueryResultSticker sticker)
            {
                if (content.Children[0] is LottieView stickerView)
                {
                    stickerView.Tag = args.Item;

                    var file = sticker.Sticker.StickerValue;
                    if (file == null)
                    {
                        return;
                    }

                    if (file.Local.IsDownloadingCompleted)
                    {
                        stickerView.Source = UriEx.ToLocal(file.Local.Path);
                    }
                    else
                    {
                        stickerView.Source = null;
                        UpdateManager.Subscribe(content, ViewModel.ClientService, file, UpdateFile, true);

                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            ViewModel.ClientService.DownloadFile(file.Id, 1);
                        }
                    }
                }
                else if (content.Children[0] is AnimationView animationView)
                {
                    animationView.Tag = args.Item;

                    var file = sticker.Sticker.StickerValue;
                    if (file == null)
                    {
                        return;
                    }

                    if (file.Local.IsDownloadingCompleted)
                    {
                        animationView.Source = new LocalVideoSource(file);
                    }
                    else
                    {
                        animationView.Source = null;
                        UpdateManager.Subscribe(content, ViewModel.ClientService, file, UpdateFile, true);

                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            ViewModel.ClientService.DownloadFile(file.Id, 1);
                        }
                    }
                }
            }
            else if (content.Children[0] is AnimationView animationView && result is InlineQueryResultAnimation animation)
            {
                animationView.Tag = args.Item;

                var file = animation.Animation.AnimationValue;
                if (file == null)
                {
                    return;
                }

                if (file.Local.IsDownloadingCompleted)
                {
                    animationView.Source = new LocalVideoSource(file);
                }
                else
                {
                    animationView.Source = null;
                    UpdateManager.Subscribe(content, ViewModel.ClientService, file, UpdateFile, true);

                    if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        ViewModel.ClientService.DownloadFile(file.Id, 1);
                    }

                    var thumbnail = animation.Animation.Thumbnail?.File;
                    if (thumbnail != null)
                    {
                        if (thumbnail.Local.IsDownloadingCompleted)
                        {
                            animationView.Thumbnail = new BitmapImage(UriEx.ToLocal(thumbnail.Local.Path));
                        }
                        else
                        {
                            animationView.Thumbnail = null;
                            UpdateManager.Subscribe(animationView, ViewModel.ClientService, thumbnail, UpdateThumbnail, true);

                            if (thumbnail.Local.CanBeDownloaded && !thumbnail.Local.IsDownloadingActive)
                            {
                                ViewModel.ClientService.DownloadFile(thumbnail.Id, 1);
                            }
                        }
                    }
                }
            }
            else if (content.Children[0] is Grid presenter)
            {
                //var presenter = content.Children[0] as Grid;
                var thumb = presenter.Children[0] as Image;

                var title = content.Children[1] as TextBlock;
                var description = content.Children[2] as TextBlock;

                File file = null;
                Uri uri = null;

                if (result is InlineQueryResultAnimation animation2)
                {
                    file = animation2.Animation.Thumbnail?.File;
                    title.Text = animation2.Title;
                    description.Text = string.Empty;
                }
                else if (result is InlineQueryResultArticle article)
                {
                    file = article.Thumbnail?.File;
                    title.Text = article.Title;
                    description.Text = article.Description;
                }
                else if (result is InlineQueryResultAudio audio)
                {
                    file = audio.Audio.AlbumCoverThumbnail?.File;
                    title.Text = audio.Audio.GetTitle();
                    description.Text = audio.Audio.GetDuration();
                }
                else if (result is InlineQueryResultContact contact)
                {
                    file = contact.Thumbnail?.File;
                    title.Text = contact.Contact.GetFullName();
                    description.Text = PhoneNumber.Format(contact.Contact.PhoneNumber);
                }
                else if (result is InlineQueryResultDocument document)
                {
                    file = document.Document.Thumbnail?.File;
                    title.Text = document.Title;

                    if (string.IsNullOrEmpty(document.Description))
                    {
                        description.Text = FileSizeConverter.Convert(document.Document.DocumentValue.Size);
                    }
                    else
                    {
                        description.Text = document.Description;
                    }
                }
                else if (result is InlineQueryResultGame game)
                {
                    file = game.Game.Animation?.Thumbnail?.File ?? game.Game.Photo.GetSmall().Photo;
                    title.Text = game.Game.Title;
                    description.Text = game.Game.Description;
                }
                else if (result is InlineQueryResultLocation location)
                {
                    var latitude = location.Location.Latitude.ToString(CultureInfo.InvariantCulture);
                    var longitude = location.Location.Longitude.ToString(CultureInfo.InvariantCulture);

                    uri = new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, 15, "96,96"));
                    file = location.Thumbnail?.File;
                    title.Text = location.Title;
                    description.Text = $"{location.Location.Latitude};{location.Location.Longitude}";
                }
                else if (result is InlineQueryResultPhoto photo)
                {
                    file = photo.Photo.GetSmall().Photo;
                    title.Text = photo.Title;
                    description.Text = photo.Description;
                }
                else if (result is InlineQueryResultVenue venue)
                {
                    var latitude = venue.Venue.Location.Latitude.ToString(CultureInfo.InvariantCulture);
                    var longitude = venue.Venue.Location.Longitude.ToString(CultureInfo.InvariantCulture);

                    uri = new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, 15, "96,96"));
                    file = venue.Thumbnail?.File;

                    title.Text = venue.Venue.Title;
                    description.Text = venue.Venue.Address;
                }
                else if (result is InlineQueryResultVideo video)
                {
                    file = video.Video.Thumbnail?.File;
                    title.Text = video.Title;
                    description.Text = video.Description;
                }
                else if (result is InlineQueryResultVoiceNote voiceNote)
                {
                    title.Text = voiceNote.Title;
                    description.Text = voiceNote.VoiceNote.GetDuration();
                }

                if (file != null)
                {
                    if (file.Local.IsDownloadingCompleted)
                    {
                        thumb.Source = new BitmapImage(UriEx.ToLocal(file.Local.Path));
                    }
                    else
                    {
                        thumb.Source = null;
                        UpdateManager.Subscribe(thumb, ViewModel.ClientService, file, UpdateThumbnail, true);

                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            ViewModel.ClientService.DownloadFile(file.Id, 1);
                        }
                    }
                }
                else if (uri != null)
                {
                    thumb.Source = new BitmapImage(uri);
                }
                else
                {
                    thumb.Source = PlaceholderHelper.GetNameForChat(title.Text, 96, title.Text.GetHashCode());
                }
            }
        }

        private async void ItemsWrapGrid_Loading(FrameworkElement sender, object args)
        {
            await sender.UpdateLayoutAsync();
            FluidGridView.Update(ScrollingHost);
        }
    }

    public class InlineQueryTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AnimatedStickerTemplate { get; set; }
        public DataTemplate VideoStickerTemplate { get; set; }
        public DataTemplate StickerTemplate { get; set; }
        public DataTemplate AnimationTemplate { get; set; }
        public DataTemplate MediaTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is InlineQueryResultSticker sticker)
            {
                return sticker.Sticker.Format switch
                {
                    StickerFormatTgs => AnimatedStickerTemplate,
                    StickerFormatWebm => VideoStickerTemplate,
                    _ => StickerTemplate
                };
            }
            else if (item is InlineQueryResultAnimation)
            {
                return AnimationTemplate;
            }

            return MediaTemplate;
        }
    }
}
