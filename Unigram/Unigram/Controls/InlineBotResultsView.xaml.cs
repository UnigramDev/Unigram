using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls
{
    public sealed partial class InlineBotResultsView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private AnimatedRepeaterHandler<InlineQueryResult> _handler;
        private ZoomableRepeaterHandler _zoomer;

        private FileContext<InlineQueryResult> _files = new FileContext<InlineQueryResult>();
        private FileContext<InlineQueryResult> _thumbnails = new FileContext<InlineQueryResult>();

        public InlineBotResultsView()
        {
            InitializeComponent();

            _handler = new AnimatedRepeaterHandler<InlineQueryResult>(Repeater, ScrollingHost);
            _handler.DownloadFile = (id, result) =>
            {
                DownloadFile(_files, id, result);
            };

            _zoomer = new ZoomableRepeaterHandler(Repeater);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ProtoService.DownloadFile(fileId, 32);
        }

        public void UpdateCornerRadius(double radius)
        {
            var min = Math.Max(4, radius - 2);

            Root.Padding = new Thickness(0, 0, 0, radius);
            SwitchPm.Radius = new CornerRadius(min, min, 4, 4);
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

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var layout = Resources["GridLayout"] as UniformGridLayout;
            if (layout == null)
            {
                return;
            }

            if (e.NewSize.Width <= 400)
            {
                layout.MaximumRowsOrColumns = 4;
            }
            else if (e.NewSize.Width <= 500)
            {
                layout.MaximumRowsOrColumns = 5;
            }
            else
            {
                layout.MaximumRowsOrColumns = (int)Math.Ceiling(e.NewSize.Width / 96);
            }
        }

        public event ItemClickEventHandler ItemClick;

        public void UpdateChatPermissions(Chat chat)
        {
            var rights = ViewModel.VerifyRights(chat, x => x.CanSendOtherMessages, Strings.Resources.GlobalAttachInlineRestricted, Strings.Resources.AttachInlineRestrictedForever, Strings.Resources.AttachInlineRestricted, out string label);

            LayoutRoot.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
            PermissionsPanel.Visibility = rights ? Visibility.Visible : Visibility.Collapsed;
            PermissionsLabel.Text = label ?? string.Empty;
        }

        public void UpdateFile(File file)
        {
            if (!file.Local.IsDownloadingCompleted)
            {
                return;
            }

            if (_thumbnails.TryGetValue(file.Id, out List<InlineQueryResult> items) && items.Count > 0)
            {
                foreach (var result in items)
                {
                    result.UpdateFile(file);

                    var index = ViewModel.InlineBotResults?.IndexOf(result) ?? -1;
                    if (index < 0)
                    {
                        continue;
                    }

                    var button = Repeater.TryGetElement(index) as Button;
                    if (button == null)
                    {
                        continue;
                    }

                    var content = button.Content as Grid;
                    if (content.Children[0] is Image image)
                    {
                        if (result is InlineQueryResultAnimation || result is InlineQueryResultPhoto || result is InlineQueryResultVideo)
                        {
                            image.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                        }
                        else if (result is InlineQueryResultSticker)
                        {
                            image.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                        }
                    }
                    else if (content.Children[0] is Grid presenter)
                    {
                        //var presenter = content.Children[0] as Grid;
                        var thumb = presenter.Children[0] as Image;
                        thumb.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                    }
                }
            }

            if (_files.TryGetValue(file.Id, out List<InlineQueryResult> items2) && items2.Count > 0)
            {
                foreach (var result in items2)
                {
                    result.UpdateFile(file);

                    var index = ViewModel.InlineBotResults?.IndexOf(result) ?? -1;
                    if (index < 0)
                    {
                        continue;
                    }

                    var button = Repeater.TryGetElement(index) as Button;
                    if (button == null)
                    {
                        continue;
                    }

                    _handler.ThrottleVisibleItems();
                }
            }

            _zoomer.UpdateFile(file);
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

        private void Result_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var result = button.DataContext as InlineQueryResult;

            Item_Click(result);
        }

        private object ConvertSource(BotResultsCollection collection)
        {
            if (collection == null)
            {
                return null;
            }
            else if (collection.All(x => x.IsMedia()) /* animation, photo, video without title */)
            {
                Repeater.Layout = Resources["MosaicLayout"] as Layout;
                Repeater.ItemTemplate = Resources["MediaTemplate"];
            }
            else if (collection.All(x => x is InlineQueryResultSticker))
            {
                Repeater.Layout = Resources["GridLayout"] as Layout;
                Repeater.ItemTemplate = Resources["StickerTemplate"];
            }
            else
            {
                Repeater.Layout = Resources["StackLayout"] as Layout;
                Repeater.ItemTemplate = Resources["ResultTemplate"];
            }

            return new object();
        }

        private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var result = button.DataContext as InlineQueryResult;

            var content = button.Content as Grid;
            if (content.Children[0] is Image image)
            {
                _zoomer.ElementPrepared(args.Element);

                if (result is InlineQueryResultAnimation || result is InlineQueryResultPhoto || result is InlineQueryResultVideo)
                {
                    File file = null;
                    if (result is InlineQueryResultAnimation animation)
                    {
                        file = animation.Animation.Thumbnail?.File;
                    }
                    else if (result is InlineQueryResultPhoto photo)
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
                        image.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                    }
                    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        image.Source = null;
                        DownloadFile(_thumbnails, file.Id, result);
                    }
                }
                else if (result is InlineQueryResultSticker sticker)
                {
                    var file = sticker.Sticker.Thumbnail.File;
                    if (file == null)
                    {
                        return;
                    }

                    if (file.Local.IsDownloadingCompleted)
                    {
                        image.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    }
                    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        image.Source = null;
                        DownloadFile(_thumbnails, file.Id, result);
                    }
                }
            }
            else if (content.Children[0] is Grid presenter)
            {
                _zoomer.ElementClearing(args.Element);

                //var presenter = content.Children[0] as Grid;
                var thumb = presenter.Children[0] as Image;

                var title = content.Children[1] as TextBlock;
                var description = content.Children[2] as TextBlock;

                File file = null;
                Uri uri = null;

                if (result is InlineQueryResultArticle article)
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
                        thumb.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                    }
                    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                    {
                        thumb.Source = null;
                        DownloadFile(_thumbnails, file.Id, result);
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

        private void OnElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            _zoomer.ElementClearing(args.Element);

            if (args.Element is Button button && button.Content is Grid content && content.Children[0] is Image image)
            {
                if (content.Children.Count > 1)
                {
                    content.Children.RemoveAt(1);
                }

                image.Source = null;
            }
        }

        private void DownloadFile<T>(FileContext<T> context, int id, T result)
        {
            context[id].Add(result);
            ViewModel.ProtoService.DownloadFile(id, 1);
        }

        private DisposableMutex _loadMoreLock = new DisposableMutex();
        private bool _loadMoreDrop;

        private async void OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var results = ViewModel.InlineBotResults;
            if (results == null || !results.HasMoreItems)
            {
                return;
            }

            if (_loadMoreDrop)
            {
                return;
            }

            using (await _loadMoreLock.WaitAsync())
            {
                _loadMoreDrop = true;

                if (ScrollingHost.ScrollableHeight - ScrollingHost.VerticalOffset < 200 && ScrollingHost.ScrollableHeight > 0 && !e.IsIntermediate)
                {
                    await results.LoadMoreItemsAsync(0);
                }

                _loadMoreDrop = false;
            }
        }
    }
}
