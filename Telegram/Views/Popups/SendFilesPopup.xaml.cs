//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Popups
{
    public sealed partial class SendFilesPopup : ContentPopup, IViewWithAutocomplete, INotifyPropertyChanged, IDiffHandler<StorageRow>
    {
        public ComposeViewModel ViewModel { get; private set; }
        public RangeObservableCollection<StorageMedia> Items { get; private set; }

        public DiffObservableCollection<StorageRow> ItemsView { get; private set; }

        private readonly StorageThumbnailCache _thumbnails = new();

        // Compiling the effect graph is the expensive half of a blur and it never varies, so every
        // spoiler brush comes from one factory. Per instance rather than static: the compositor
        // belongs to the window.
        private CompositionEffectFactory _blurFactory;

        // A container walk is already waiting on layout; see UpdatePanel.
        private bool _panelPending;

        private readonly StorageMediaSource _source;
        private readonly CancellationTokenSource _loadCancellation = new();

        // Returning false abandons the whole drop, which is what the guard this replaced did
        // before the popup was shown. Null when the caller has nothing to enforce.
        private readonly Func<StorageMedia, bool> _validating;

        // Resolved but not yet published, keyed by position in the source. Probing finishes out of
        // order, so this holds results back until the run in front of them has settled.
        private readonly Dictionary<int, StorageMedia> _resolved = new();
        private int _published;
        private bool _flushScheduled;

        private bool _loadStarted;

        // Batches in flight. More than one when files are dropped onto the popup while the batch
        // it opened for is still being typed.
        private int _loading;
        private bool IsLoading => _loading > 0;

        // Next free slot in the index space. Only ever grows, so a batch appended later lands
        // behind everything already picked.
        private int _allocated;

        // What the title claims while items are still arriving. Overstates by however many turn
        // out to be untypable, and drops to zero once nothing is in flight so the title falls back
        // to what is actually listed.
        private int _expected;
        private bool _mediaRequested;

        private IAutocompleteCollection _autocomplete;
        public IAutocompleteCollection Autocomplete
        {
            get => _autocomplete;
            set
            {
                if (_autocomplete != value)
                {
                    _autocomplete = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Autocomplete)));
                }
            }
        }

        private readonly bool _photoAllowed;
        private readonly bool _videoAllowed;
        private readonly bool _audioAllowed;
        private readonly bool _documentAllowed;

        public bool IsMediaAllowed
        {
            get
            {
                if (_photoAllowed && Items.Any(x => x is StoragePhoto))
                {
                    return true;
                }
                else if (_videoAllowed && Items.Any(x => x is StorageVideo))
                {
                    return true;
                }
                else if (_audioAllowed && Items.Any(x => x is StorageAudio))
                {
                    return true;
                }

                return false;
            }
        }

        private readonly bool _editing;

        private readonly bool _ttlAllowed;
        public bool IsTtlAvailable => _ttlAllowed && Items.Count == 1;

        public bool HasPaidMediaAllowed { get; set; }

        private bool _isMediaSelected;
        public bool IsMediaSelected
        {
            get => _isMediaSelected;
            set
            {
                if (_isMediaSelected != value)
                {
                    _isMediaSelected = value;
                    _isFilesSelected = !value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMediaSelected)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFilesSelected)));
                }
            }
        }

        private bool _isFilesSelected;
        public bool IsFilesSelected
        {
            get => _isFilesSelected;
            set
            {
                if (_isFilesSelected != value)
                {
                    _isFilesSelected = value;
                    _isMediaSelected = !value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFilesSelected)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMediaSelected)));
                }
            }
        }

        public string TitleText
        {
            get
            {
                // Count what is still coming, or the title ticks upwards one item at a time. Their
                // type is not known until they land, so they only count as files.
                var count = Math.Max(Items.Count, _expected);

                if (IsMediaSelected && _expected == 0)
                {
                    if (Items.All(x => x is StoragePhoto))
                    {
                        return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Photos, count));
                    }
                    else if (Items.All(x => x is StorageVideo))
                    {
                        return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Videos, count));
                    }
                    else if (Items.All(x => x is StoragePhoto or StorageVideo))
                    {
                        return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Media, count));
                    }
                }

                return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Files, count));
            }
        }

        public bool IsAlbum { get; private set; } = true;

        private bool _showCaptionAboveMedia;
        public bool ShowCaptionAboveMedia
        {
            get => _showCaptionAboveMedia;
            set
            {
                if (_showCaptionAboveMedia != value)
                {
                    _showCaptionAboveMedia = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowCaptionAboveMedia)));
                }
            }
        }

        private bool _sendWithSpoiler;
        public bool SendWithSpoiler
        {
            get => _sendWithSpoiler;
            set
            {
                if (_sendWithSpoiler != value)
                {
                    _sendWithSpoiler = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendWithSpoiler)));
                }
            }
        }

        private bool _sendHighQuality;
        public bool SendHighQuality
        {
            get => _sendHighQuality;
            set
            {
                if (_sendHighQuality != value)
                {
                    _sendHighQuality = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendHighQuality)));
                }
            }
        }

        private long _starCount;
        public long StarCount
        {
            get => _starCount;
            set
            {
                if (_starCount != value)
                {
                    _starCount = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StarCount)));
                }
            }
        }

        public FormattedText Caption
        {
            get => CaptionInput.GetFormattedText(false);
            set => CaptionInput.SetText(value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool CanSchedule { get; set; }
        public bool IsSavedMessages { get; set; }

        public SchedulingState Schedule { get; private set; }
        public bool? Silent { get; private set; }

        public long PaidMessageStarCount { get; private set; }

        public SendFilesPopup(ComposeViewModel viewModel, StorageMediaSource source, Func<StorageMedia, bool> validating, bool media, ChatPermissions permissions, bool ttlAllowed, bool schedule, bool savedMessages, bool editing)
        {
            InitializeComponent();

            _source = source;
            _validating = validating;

            if (source.IsComplete)
            {
                LogContent(source.Ready);
            }
            else
            {
                // Nothing is typed yet, so only the size of what is coming can be recorded here.
                // LogContent runs again once it has all landed.
                Logger.Info(string.Format("{0} pending", source.Count));
            }

            IsSavedMessages = savedMessages;
            CanSchedule = schedule;

            _editing = editing;
            _ttlAllowed = ttlAllowed;
            _photoAllowed = permissions.CanSendPhotos;
            _videoAllowed = permissions.CanSendVideos;
            _audioAllowed = permissions.CanSendAudios;
            _documentAllowed = permissions.CanSendDocuments;

            DataContext = viewModel;
            ViewModel = viewModel;

            ItemsView = new DiffObservableCollection<StorageRow>(this);

            // Seeded rather than loaded, so a caller handing over typed items can read Items back
            // the moment the popup closes. Only what the source still owes arrives later.
            Items = new RangeObservableCollection<StorageMedia>(source.Ready);
            Items.CollectionChanged += OnCollectionChanged;

            // Slots the source already filled are published; the rest are the load's to hand out.
            _allocated = source.Ready.Count;
            _published = source.Ready.Count;
            _expected = source.IsComplete ? 0 : source.Count;

            // With nothing typed yet IsMediaAllowed cannot answer, so UpdateView re-derives the
            // mode as the first items land.
            _mediaRequested = media;
            IsMediaSelected = media && IsMediaAllowed;
            IsFilesSelected = !IsMediaSelected;

            SendHighQuality = AppSettings.SendLargePhotos;

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(viewModel.Session);
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.ViewModel = viewModel;

            if (viewModel.ClientService.TryGetUserFull(viewModel.Chat, out UserFullInfo userFull))
            {
                PaidMessageStarCount = userFull.OutgoingPaidMessageStarCount;
            }
            else if (viewModel.ClientService.TryGetSupergroup(viewModel.Chat, out Supergroup supergroup))
            {
                PaidMessageStarCount = supergroup.PaidMessageStarCount;
            }

            if (PaidMessageStarCount > 0)
            {
                SendMessage.Visibility = Visibility.Collapsed;
                PaidMessage.Visibility = Visibility.Visible;
            }
            else
            {
                SendMessage.Visibility = Visibility.Visible;
                PaidMessage.Visibility = Visibility.Collapsed;
            }

            AddButton.Visibility = editing
                ? Visibility.Collapsed
                : Visibility.Visible;

            MoreButton.Margin = new Thickness(0, -62, editing ? 40 : 80, 0);

            UpdateView();
            UpdatePanel();
        }

        /// <summary>
        /// Records the dimensions of every item. This line ships with crash reports and is what
        /// album layout bugs get diagnosed from, so it has to name sizes rather than counts.
        /// </summary>
        private static void LogContent(IEnumerable<StorageMedia> items)
        {
            var builder = new StringBuilder();

            foreach (var item in items)
            {
                switch (item)
                {
                    case StoragePhoto photo:
                        builder.Prepend(string.Format("photo {0}x{1}", photo.Width, photo.Height), ", ");
                        break;
                    case StorageVideo video:
                        builder.Prepend(string.Format("video {0}x{1}", video.Width, video.Height), ", ");
                        break;
                    default:
                        builder.Prepend("file", ", ");
                        break;
                }
            }

            Logger.Info(builder);
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleText)));

            if (Items.Count > 0)
            {
                UpdateView();
                UpdatePanel();
            }
            else
            {
                Hide();
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SetResult(ContentDialogResult.Primary);
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SetResult(ContentDialogResult.Secondary);
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var selection = CaptionInput.Document.Selection.GetClone();
            var entity = AutocompleteEntityFinder.Search(selection, out string result, out int index);

            if (e.ClickedItem is User user && entity == AutocompleteEntity.Username)
            {
                var username = user.ActiveUsername(result);

                string insert;
                if (string.IsNullOrEmpty(username))
                {
                    insert = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;

                    if (FormattedTextBox.IsUnsafe(insert))
                    {
                        insert = Strings.Username;
                    }
                }
                else
                {
                    insert = $"@{username}";
                }

                var range = CaptionInput.Document.GetRange(index, CaptionInput.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                if (string.IsNullOrEmpty(username))
                {
                    range.Link = $"\"tg-user://{user.Id}\"";
                }

                CaptionInput.Document.GetRange(range.EndPosition, range.EndPosition).SetText(TextSetOptions.None, " ");
                CaptionInput.Document.Selection.StartPosition = range.EndPosition + 1;
            }
            else if (e.ClickedItem is EmojiData or Sticker && entity == AutocompleteEntity.Emoji)
            {
                if (e.ClickedItem is EmojiData emoji)
                {
                    var insert = $"{emoji.Value} ";
                    var start = CaptionInput.Document.Selection.StartPosition - 1 - result.Length + insert.Length;
                    var range = CaptionInput.Document.GetRange(CaptionInput.Document.Selection.StartPosition - 1 - result.Length, CaptionInput.Document.Selection.StartPosition);
                    range.SetText(TextSetOptions.None, insert);

                    CaptionInput.Document.Selection.StartPosition = start;
                }
                else if (e.ClickedItem is Sticker sticker && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
                {
                    var start = CaptionInput.Document.Selection.StartPosition - 1 - result.Length + 1;
                    var range = CaptionInput.Document.GetRange(CaptionInput.Document.Selection.StartPosition - 1 - result.Length, CaptionInput.Document.Selection.StartPosition);

                    CaptionInput.InsertEmoji(range, sticker.Emoji, customEmoji.CustomEmojiId);
                    CaptionInput.Document.Selection.StartPosition = range.EndPosition + 1;
                }
            }

            Autocomplete = null;
        }

        private void Autocomplete_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is User user)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var photo = content.Children[0] as ProfilePicture;
                var title = content.Children[1] as TextBlock;

                var name = title.Inlines[0] as Run;
                var username = title.Inlines[1] as Run;

                name.Text = user.FullName();

                if (user.HasActiveUsername(out string usernameValue))
                {
                    username.Text = $" @{usernameValue}";
                }
                else
                {
                    username.Text = string.Empty;
                }

                photo.Source = ProfilePictureSource.User(ViewModel.ClientService, user);
            }
            else if (args.Item is Sticker sticker)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var animated = content.Children[0] as AnimatedImage;
                animated.Source = new DelayedFileSource(ViewModel.ClientService, sticker);

                AutomationProperties.SetName(args.ItemContainer, sticker.Emoji);
            }
            else if (args.Item is EmojiData emoji)
            {
                AutomationProperties.SetName(args.ItemContainer, emoji.Value);
            }

            args.Handled = true;
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var above = ShowCaptionAboveMedia;
            var visible = e.NewSize.Height > 0;

            CaptionInput.CornerRadius = new CornerRadius(above ? 2 : visible ? 0 : 2, above ? 2 : visible ? 0 : 2, above ? visible ? 0 : 2 : 2, above ? visible ? 0 : 2 : 2);
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                // The container keeps its template while queued, so the ImageBrush would go on
                // rooting the decoded bitmap. Read the media off the panel rather than args.Item,
                // which is not guaranteed to survive into the recycle notification.
                if (args.ItemContainer.ContentTemplateRoot is Grid recycled
                    && recycled.Children.Count > 0
                    && recycled.Children[0] is MosaicPanel recycledPanel)
                {
                    ReleaseThumbnails(recycledPanel);
                }

                return;
            }

            args.Handled = true;

            var root = args.ItemContainer.ContentTemplateRoot as Grid;
            if (root == null)
            {
                return;
            }

            if (args.Item is MosaicRow mosaic)
            {
                if (root.Children[0] is MosaicPanel panel)
                {
                    UpdatePaidMedia(root);

                    panel.UpdateMessage(mosaic);
                }

                return;
            }

            if (args.Item is not FileRow file)
            {
                return;
            }

            var storage = file.Media;

            var glyph = root.FindName("Glyph");
            if (glyph is AnimatedGlyphButton animated)
            {
                // The item itself, not a wrapper standing in for it: Remove_Click looks this up in
                // Items, and in files mode the wrapper was never in there to be found.
                animated.Tag = storage;
                animated.Glyph = Icons.DocumentFilled24;
            }
            else if (glyph is TextBlock text)
            {
                text.Text = GlyphFor(file);
            }

            var title = root.FindName("Title") as TextBlock;
            var titleTrim = root.FindName("TitleTrim") as TextBlock;
            var subtitle = root.FindName("Subtitle") as TextBlock;

            if (title == null || titleTrim == null || subtitle == null)
            {
                return;
            }

            // Files mode names the file rather than the track, as it always has.
            if (!file.AsDocument && storage is StorageAudio audio)
            {
                if (string.IsNullOrEmpty(audio.Performer) || string.IsNullOrEmpty(audio.Title))
                {
                    var index = storage.File.Name.LastIndexOf('.');
                    if (index > 0)
                    {
                        title.Text = storage.File.Name.Substring(0, index + 1);
                        titleTrim.Text = storage.File.Name.Substring(index + 1);
                    }
                    else
                    {
                        title.Text = storage.File.Name;
                        titleTrim.Text = string.Empty;
                    }
                }
                else
                {
                    title.Text = $"{audio.Performer} - {audio.Title}";
                    titleTrim.Text = string.Empty;
                }

                subtitle.Text = audio.Duration;
                subtitle.Visibility = Visibility.Visible;
            }
            else
            {
                var index = storage.File.Name.LastIndexOf('.');
                if (index > 0)
                {
                    title.Text = storage.File.Name.Substring(0, index + 1);
                    titleTrim.Text = storage.File.Name.Substring(index + 1);
                }
                else
                {
                    title.Text = storage.File.Name;
                    titleTrim.Text = string.Empty;
                }

                if (storage.Size > 0)
                {
                    subtitle.Text = FileSizeConverter.Convert((long)storage.Size);
                    subtitle.Visibility = Visibility.Visible;
                }
                else
                {
                    subtitle.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void FileItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var content = sender as Grid;

            var glyph = content.FindName("Glyph") as AnimatedGlyphButton;
            glyph?.Glyph = Icons.DeleteFilled24;
        }

        private void FileItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var content = sender as Grid;

            var glyph = content.FindName("Glyph") as AnimatedGlyphButton;
            glyph?.Glyph = Icons.DocumentFilled24;
        }

        private static string GlyphFor(FileRow row)
        {
            if (row.AsDocument)
            {
                return Icons.DocumentFilled24;
            }

            return row.Media is StoragePhoto
                ? Icons.ImageFilled24
                : row.Media is StorageVideo or StorageAudio
                ? Icons.PlayFilled24
                : Icons.DocumentFilled24;
        }

        private void MediaItem_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var content = sender as Grid;
            var rootGrid = content.FindName("RootGrid") as Grid;

            rootGrid.Opacity = 1;
        }

        private void MediaItem_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var content = sender as Grid;
            var rootGrid = content.FindName("RootGrid") as Grid;

            rootGrid.Opacity = 0;
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.Parent is Grid root && root.DataContext is StorageMedia storage)
            {
                UpdateTemplate(root, storage);
            }
        }

        private void Grid_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (sender.Parent is Grid root && args.NewValue is StorageMedia storage)
            {
                UpdateTemplate(root, storage);
            }
        }

        private void UpdateTemplate(Grid root, StorageMedia storage)
        {
            UpdateThumbnail(root, storage);

            var overlay = root.FindName("Overlay") as Border;
            overlay.Visibility = storage is StorageVideo ? Visibility.Visible : Visibility.Collapsed;

            var mute = root.FindName("Mute") as ToggleButton;
            var crop = root.FindName("Crop") as ToggleButton;
            var ttl = root.FindName("Ttl") as ToggleButton;

            if (mute == null)
            {
                return;
            }

            if (storage is StorageVideo video)
            {
                mute.IsChecked = video.IsMuted;
                mute.Visibility = Visibility.Visible;
            }
            else
            {
                mute.Visibility = Visibility.Collapsed;
            }

            crop.Visibility = storage is StoragePhoto ? Visibility.Visible : Visibility.Collapsed;
            ttl.Visibility = IsTtlAvailable ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateThumbnail(Grid root, StorageMedia storage)
        {
            if (storage == null || root.Background is not ImageBrush brush)
            {
                return;
            }

            if (_thumbnails.TryGet(storage, out var cached))
            {
                brush.ImageSource = cached;
                return;
            }

            brush.ImageSource = null;
            LoadThumbnail(root, storage);
        }

        private async void LoadThumbnail(Grid root, StorageMedia storage)
        {
            var source = await _thumbnails.GetAsync(storage);

            // The container may have been recycled onto another item, or torn down entirely,
            // while the decode was in flight.
            if (root.DataContext == storage && root.Background is ImageBrush brush)
            {
                brush.ImageSource = source;
            }
        }

        private void ReleaseThumbnails(MosaicPanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is ContentControl { ContentTemplateRoot: Grid inner } && inner.Background is ImageBrush brush)
                {
                    brush.ImageSource = null;
                }
            }

            _thumbnails.Release(panel.Media);
        }

        public void Accept()
        {
            // Reachable from Enter even while the send button is disabled.
            if (IsLoading)
            {
                return;
            }

            if (CaptionInput.HandwritingView.IsOpen)
            {
                void handler(object s, RoutedEventArgs args)
                {
                    CaptionInput.HandwritingView.Unloaded -= handler;

                    Caption = CaptionInput.GetFormattedText();
                    Hide(ContentDialogResult.Primary);
                }

                CaptionInput.HandwritingView.Unloaded += handler;
                CaptionInput.HandwritingView.TryClose();
            }
            else
            {
                Caption = CaptionInput.GetFormattedText();
                Hide(ContentDialogResult.Primary);
            }
        }

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            var content = ClipboardEx.TryGetContent();
            if (content == null)
            {
                return;
            }

            if (content.AvailableFormats.Contains(StandardDataFormats.Text))
            {
                e.Handled = true;
            }
            else
            {
                e.Handled = true;
                await HandlePackageAsync(content);
            }
        }

        private void ListView_DragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private async void ListView_Drop(object sender, DragEventArgs e)
        {
            await HandlePackageAsync(e.DataView);
        }

        /// <summary>
        /// Drains whatever the source did not hand over at construction, appending items as they
        /// land. Driven from the popup's own Loaded so a caller cannot forget it — and only once
        /// on screen, since a rejected item closes the popup and OpenAsync queues behind any other
        /// dialog, leaving an earlier Hide with nothing to close.
        /// </summary>
        private void Load()
        {
            // Loaded fires again whenever the popup is re-parented.
            if (_loadStarted || _source.IsComplete)
            {
                return;
            }

            _loadStarted = true;

            // The mode gates the album-boundary hold-back in Flush, so it decides whether the
            // popup sits on screen empty while the files type. A crash from that window cannot
            // be read without it.
            Logger.Info(string.Format("Requested: {0}, media: {1}, count: {2}", _mediaRequested, IsMediaSelected, _source.Count));

            // The source owns the first stretch of the index space; anything appended later
            // continues past it, so appended files land behind what was already picked.
            LoadAsync(_source.LoadAsync, _source.Count, true);
        }

        /// <summary>
        /// Types a batch into the tail of the index space and publishes it as it lands. Batches can
        /// overlap — dropping onto the popup while the first one is still going is ordinary — so
        /// the loading state is a count rather than a flag.
        /// </summary>
        /// <param name="initial">
        /// The batch the popup was opened for, which is the one the caller's guard applies to and
        /// the only one whose emptiness closes the popup. A batch added afterwards leaves whatever
        /// is already listed alone, and is no more checked than it was before it streamed — see
        /// 6.6 in sendfiles-popup-todo.md.
        /// </param>
        private async void LoadAsync(Func<Action<int, StorageMedia>, CancellationToken, Task> load, int count, bool initial)
        {
            var offset = _allocated;

            _allocated += count;
            _expected = Math.Max(_expected, _allocated);
            _loading++;
            UpdateLoading();

            void Resolved(int index, StorageMedia media)
            {
                OnResolved(offset + index, media, initial);
            }

            await load(Resolved, _loadCancellation.Token);

            if (_loadCancellation.IsCancellationRequested)
            {
                return;
            }

            // Publishes the tail, including a part-filled last album. Before the count drops, so
            // the last batch still reaches UpdateView with a load in progress and can settle the
            // media/files mode.
            Flush(true);

            _loading--;

            if (_loading == 0)
            {
                _expected = 0;
            }

            UpdateLoading();

            LogContent(Items);

            if (initial && Items.Count == 0)
            {
                Hide();
            }
        }

        private void AppendFiles(IReadOnlyList<StorageFile> files)
        {
            LoadAsync((resolved, cancellationToken) => StorageMedia.ProbeAsync(files, resolved, cancellationToken), files.Count, false);
        }

        private void OnResolved(int index, StorageMedia media, bool validate)
        {
            if (validate && media != null && _validating?.Invoke(media) == false)
            {
                _loadCancellation.Cancel();
                Hide();

                return;
            }

            // Null when the file could not be typed. The slot is still recorded, or the run would
            // never get past it.
            _resolved[index] = media;

            if (_flushScheduled)
            {
                return;
            }

            _flushScheduled = true;
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, Flush);
        }

        private void Flush()
        {
            Flush(false);
        }

        /// <summary>
        /// Publishes the longest run of items that have settled in source order. Out-of-order
        /// results wait: album membership is positional, so a slot that has not settled could still
        /// turn out to be a document and split the album behind it.
        /// </summary>
        /// <param name="final">
        /// Publishes the tail. Until then, while media are still arriving, only whole albums are
        /// published — otherwise an album visibly reflows as each photo lands.
        /// </param>
        private void Flush(bool final)
        {
            _flushScheduled = false;

            if (_loadCancellation.IsCancellationRequested)
            {
                return;
            }

            var run = 0;

            while (_resolved.ContainsKey(_published + run))
            {
                run++;
            }

            var count = run;

            if (!final && IsLoading && _mediaRequested)
            {
                // Everything that completes an album rather than one album per pass, so a drop
                // that types quickly still lands in a single flush.
                count -= count % StorageAlbum.MAX_ITEMS;
            }

            if (count == 0)
            {
                // run ahead of count is the difference between nothing having typed yet and a
                // part-filled album being held back, and both leave the list empty.
                Logger.Info(string.Format("Nothing to publish, resolved: {0}, published: {1}", run, _published));
                return;
            }

            var length = 0;

            for (int i = 0; i < count; i++)
            {
                if (_resolved[_published + i] != null)
                {
                    length++;
                }
            }

            var media = new StorageMedia[length];
            var next = 0;

            for (int i = 0; i < count; i++)
            {
                var index = _published + i;

                if (_resolved[index] is StorageMedia item)
                {
                    media[next++] = item;
                }

                _resolved.Remove(index);
            }

            _published += count;

            Logger.Info(string.Format("Published {0} of {1}, final: {2}", length, count, final));

            if (length > 0)
            {
                // One CollectionChanged for the batch, so one UpdateView and one UpdatePanel.
                Items.AddRange(media);
            }
        }

        private void UpdateLoading()
        {
            // Sending half a drop would silently drop the rest, so the button waits.
            SendMessage.IsEnabled = !IsLoading;
            PaidMessage.IsEnabled = !IsLoading;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleText)));
        }

        public async Task HandlePackageAsync(DataPackageView package)
        {
            try
            {
                if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                {
                    var photo = await StorageMedia.CreateFromBitmapAsync(package);
                    if (photo != null)
                    {
                        if (_editing)
                        {
                            Items.ReplaceWith(new[] { photo });
                        }
                        else
                        {
                            Items.Add(photo);
                        }

                        UpdateView();
                        UpdatePanel();
                    }
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    var files = await StorageMedia.GetFilesAsync(package);
                    if (files.Count == 0)
                    {
                        return;
                    }

                    if (_editing)
                    {
                        // Editing replaces one message, so there is nothing to stream in.
                        var replacement = await StorageMedia.CreateAsync(files[0]);
                        if (replacement != null)
                        {
                            Items.ReplaceWith(new[] { replacement });

                            UpdateView();
                            UpdatePanel();
                        }
                    }
                    else
                    {
                        AppendFiles(files);
                    }
                }
            }
            catch (Exception ex)
            {
                // The package is someone else's data and every read of it is a remote call, so
                // this has to keep swallowing — but silently meant a paste that did nothing left
                // nothing to look at.
                Logger.Error(ex);
            }
        }

        private void UpdateView()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMediaAllowed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleText)));

            // The constructor could not honour the requested mode against an empty list, so it is
            // settled here instead — but only until the user picks a mode of their own.
            if (IsLoading && _mediaRequested && !IsMediaSelected && IsMediaAllowed)
            {
                IsMediaSelected = true;
                IsFilesSelected = false;
            }

            if (IsMediaSelected && !IsMediaAllowed && _documentAllowed)
            {
                IsMediaSelected = false;
                IsFilesSelected = true;
            }

            MoreButton.Visibility = Items.Any(x => x is StoragePhoto or StorageVideo or StorageAudio)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateCollection()
        {
            var view = new List<StorageRow>();

            // The same grouping the send path uses, so an album on screen is a message that will
            // be sent. The popup had its own, which split only on the ten-item limit and so showed
            // one album where sending made two — it knew nothing of muted video, of the WEBP
            // workaround, or of not mixing media with documents and audio.
            //
            // Permissions go in as allowed rather than as the chat's: everything in Items already
            // cleared the guard in SendFilesAsync, and the edit path has no guard at all, so
            // filtering here could only blank out an item the popup exists to show.
            var grouped = ComposeViewModel.GetItemsView(Items, IsAlbum, IsFilesSelected, true, true, true, true);

            // Counted over mosaic rows only, so a file row appearing or going between two of them
            // does not renumber everything after it and force a rebuild.
            var mosaics = 0;

            foreach (var item in grouped)
            {
                if (item is StorageAlbum album)
                {
                    if (album.Type is StorageAlbumType.Media or StorageAlbumType.NotSupported)
                    {
                        view.Add(new MosaicRow(mosaics++, album.Media));
                        continue;
                    }

                    // Documents and audio have no mosaic, so their grouping is invisible either
                    // way and they stay the rows they have always been.
                    foreach (var media in album.Media)
                    {
                        view.Add(new FileRow(media, IsFilesSelected));
                    }
                }
                else if (!IsFilesSelected && item is StoragePhoto or StorageVideo)
                {
                    // The grouping leaves a muted video on its own because it is sent as its own
                    // message. It is still a video, so it draws as a mosaic of one.
                    view.Add(new MosaicRow(mosaics++, new[] { item }));
                }
                else
                {
                    view.Add(new FileRow(item, IsFilesSelected));
                }
            }

            ItemsView.CollectionChanged -= ItemsView_CollectionChanged;
            ItemsView.ReplaceDiff(view);
            ItemsView.CollectionChanged += ItemsView_CollectionChanged;

            if (PaidMessageStarCount > 0)
            {
                PaidMessage.Content = Icons.Premium16 + Icons.Spacing + Formatter.ShortNumber(PaidMessageStarCount * Items.Count);
            }
        }

        private async void UpdatePanel()
        {
            UpdateCollection();

            if (ScrollingHost.ItemsPanelRoot is ItemsStackPanel panel && IsAlbum)
            {
                void UpdateSelectorItem(Grid content)
                {
                    if (content.Children[0] is MosaicPanel mosaic)
                    {
                        UpdatePaidMedia(content);

                        foreach (var child in mosaic.Children)
                        {
                            if (child is ContentControl { ContentTemplateRoot: Grid inner })
                            {
                                UpdateSelectorItem(inner);
                            }
                        }

                        return;
                    }

                    UpdateTemplate(content, content.DataContext as StorageMedia);

                    var obscured = SendWithSpoiler || StarCount > 0;

                    // Both of these are reached on every interaction, so they only touch the tree
                    // when the state they represent actually flipped.
                    if (content.FindName("Particles") is AnimatedImage particles && (particles.Source is ParticlesImageSource) != obscured)
                    {
                        particles.Source = obscured
                            ? new ParticlesImageSource()
                            : null;
                    }

                    if (content.FindName("BackDrop") is Border border && (ElementCompositionPreview.GetElementChildVisual(border) != null) != obscured)
                    {
                        ElementCompositionPreview.SetElementChildVisual(border, obscured ? CreateBlurVisual() : null);
                    }
                }

                // One walk per layout pass. The album panels each raise Loading and every arriving
                // batch raises this again, so the calls arrive in bursts — and the walk reads the
                // live state rather than anything captured here, so the one already waiting covers
                // whatever the callers behind it changed. The flag clears before the walk, so a
                // call that arrives during it still gets one of its own.
                if (_panelPending)
                {
                    return;
                }

                _panelPending = true;

                await ScrollingHost.UpdateLayoutAsync();

                _panelPending = false;

                ScrollingHost.ForEach<StorageRow>((selector, item) =>
                {
                    if (item is MosaicRow && selector.ContentTemplateRoot is Grid content)
                    {
                        UpdateSelectorItem(content);
                    }
                });
            }
        }

        private SpriteVisual CreateBlurVisual()
        {
            var compositor = BootStrapper.Current.Compositor;

            _blurFactory ??= compositor.CreateEffectFactory(new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 3,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("Backdrop")
            }, new[] { "Blur.BlurAmount" });

            var effectBrush = _blurFactory.CreateBrush();
            effectBrush.SetSourceParameter("Backdrop", compositor.CreateBackdropBrush());

            var blurVisual = compositor.CreateSpriteVisual();
            blurVisual.RelativeSizeAdjustment = Vector2.One;
            blurVisual.Brush = effectBrush;

            return blurVisual;
        }

        private void UpdatePaidMedia(Grid root)
        {
            var PaidMediaButton = root.FindName("PaidMediaButton") as FrameworkElement;

            if (StarCount > 0)
            {
                var text = Locale.Declension(Strings.R.UnlockPaidContent, StarCount);
                var index = text.IndexOf("\u2B50\uFE0F");

                var TextPart1 = root.FindName("TextPart1") as Run;
                var TextPart2 = root.FindName("TextPart2") as Run;

                TextPart1.Text = text.Substring(0, index);
                TextPart2.Text = text.Substring(index + 2);

                PaidMediaButton.Visibility = Visibility.Visible;
            }
            else
            {
                PaidMediaButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ItemsView_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            var items = new List<StorageMedia>();

            foreach (var row in ItemsView)
            {
                if (row is MosaicRow mosaic)
                {
                    items.AddRange(mosaic.Media);
                }
                else if (row is FileRow file)
                {
                    items.Add(file.Media);
                }
            }

            Items.CollectionChanged -= OnCollectionChanged;
            Items.ReplaceWith(items);
            Items.CollectionChanged += OnCollectionChanged;

            UpdateCollection();
        }

        private void PivotRadioButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateView();
            UpdatePanel();
        }

        private void Album_Click(object sender, RoutedEventArgs e)
        {
            //_wasAlbum = AlbumButton.IsChecked == true;
            //IsAlbum = _wasAlbum;

            UpdateView();
            UpdatePanel();
        }

        private void SendFilesAlbumPanel_Loading(FrameworkElement sender, object args)
        {
            UpdatePanel();
        }

        private void Ttl_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            var media = button.Tag as StorageMedia;

            var flyout = new MenuFlyout();

            flyout.Items.Add(new MenuFlyoutLabel
            {
                Text = Strings.TimerPeriodHint,
                Padding = new Thickness(12, 4, 12, 8),
                MaxWidth = 178,
            });

            flyout.Items.Add(new MenuFlyoutSeparator());

            void Update(MessageSelfDestructType ttl)
            {
                media.Ttl = ttl;
                ToastPopup.Show(XamlRoot,
                    media is StorageVideo
                        ? ttl is MessageSelfDestructTypeTimer timer1
                        ? Locale.Declension(Strings.R.TimerPeriodVideoSetSeconds, timer1.SelfDestructTime)
                        : ttl is MessageSelfDestructTypeImmediately
                        ? Strings.TimerPeriodVideoSetOnce
                        : Strings.TimerPeriodVideoKeep
                        : ttl is MessageSelfDestructTypeTimer timer2
                        ? Locale.Declension(Strings.R.TimerPeriodPhotoSetSeconds, timer2.SelfDestructTime)
                        : ttl is MessageSelfDestructTypeImmediately
                        ? Strings.TimerPeriodPhotoSetOnce
                        : Strings.TimerPeriodPhotoKeep,
                    ttl == null
                        ? ToastPopupIcon.AutoRemoveOff
                        : ToastPopupIcon.AutoRemoveOn);

                UpdateView();
                UpdatePanel();
            }

            var command = new RelayCommand<MessageSelfDestructType>(Update);

            void CreateToggle(MessageSelfDestructType value, string text)
            {
                var toggle = new ToggleMenuFlyoutItem
                {
                    Text = text,
                    IsChecked = value.AreTheSame(media.Ttl),
                    Command = command,
                    CommandParameter = value
                };

                flyout.Items.Add(toggle);
            }

            CreateToggle(new MessageSelfDestructTypeImmediately(), Strings.TimerPeriodOnce);
            CreateToggle(new MessageSelfDestructTypeTimer(3), Locale.Declension(Strings.R.Seconds, 3));
            CreateToggle(new MessageSelfDestructTypeTimer(10), Locale.Declension(Strings.R.Seconds, 10));
            CreateToggle(new MessageSelfDestructTypeTimer(30), Locale.Declension(Strings.R.Seconds, 30));
            CreateToggle(new MessageSelfDestructTypeTimer(60), Locale.Declension(Strings.R.Seconds, 60));
            CreateToggle(null, Strings.TimerPeriodDoNotDelete);

            flyout.ShowAt(button.Parent, FlyoutPlacementMode.TopEdgeAlignedRight);
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            if (button.Tag is StorageVideo video)
            {
                button.IsChecked = !button.IsChecked == true;
                video.IsMuted = button.IsChecked == true;

                // Muting takes the video out of its album, since it is sent as its own message.
                // Nothing binds IsMuted, so the grouping only changes if it is asked to.
                UpdatePanel();
            }
        }

        private async void Crop_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            if (button.Tag is StorageMedia media)
            {
                var parent = button.GetParent<AspectView>();
                if (parent != null)
                {
                    ConnectedAnimationServiceEx.PrepareToAnimate("EditMediaPopup", parent);
                }

                var popup = new EditMediaPopup(XamlRoot, media);

                var confirm = await popup.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    _thumbnails.Invalidate(media);

                    UpdateView();
                    UpdatePanel();
                }
            }
        }

        private async void Album_ItemClick(object sender, StorageMedia args)
        {
            var parent = sender as UIElement;
            if (parent != null)
            {
                ConnectedAnimationServiceEx.PrepareToAnimate("EditMediaPopup", parent);
            }

            var popup = new EditMediaPopup(XamlRoot, args);

            var confirm = await popup.ShowAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _thumbnails.Invalidate(args);

                UpdateView();
                UpdatePanel();
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Tag is StorageMedia media)
            {
                // The album panel reuses its children now, so a removed item's thumbnail is no
                // longer released by a container recycling underneath it.
                _thumbnails.Invalidate(media);

                Items.Remove(media);
            }
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            Accept();
        }

        private void Send_ContextRequested(object sender, ContextRequestedEventArgs args)
        {
            if (CanSchedule)
            {
                var self = IsSavedMessages;

                var flyout = new MenuFlyout();

                // If number of items is different from the view then there's some album.
                // The real flags, not a fixed pair: this predicts what sending would do, so it has
                // to ask the same question SendFilesAsync will.
                var itemsView = ComposeViewModel.GetItemsView(Items, IsAlbum, IsFilesSelected, _photoAllowed, _videoAllowed, _audioAllowed, _documentAllowed);
                if (itemsView.Count < Items.Count)
                {
                    flyout.CreateFlyoutItem(SendWithoutGrouping, Strings.SendWithoutGrouping, "\uE90C");
                }

                flyout.CreateFlyoutItem(SendWithoutSound, Strings.SendWithoutSound, Icons.AlertOff);
                flyout.CreateFlyoutItem(SendScheduled, self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);

                flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.TopEdgeAlignedRight);
            }
        }

        private void SendWithoutGrouping()
        {
            IsAlbum = false;
            Hide(ContentDialogResult.Primary);
        }

        private void SendWithoutSound()
        {
            Silent = true;
            Hide(ContentDialogResult.Primary);
        }

        private void SendScheduled()
        {
            Schedule = SchedulingState.Schedule;
            Hide(ContentDialogResult.Primary);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CaptionInput.Focus(FocusState.Keyboard);
            CharacterReceived += OnCharacterReceived;
            PreviewKeyDown += OnPreviewKeyDown;

            Load();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CharacterReceived -= OnCharacterReceived;
            PreviewKeyDown -= OnPreviewKeyDown;

            // The items outlive the popup — they are handed to the send loop — so the thumbnails
            // have to be dropped here rather than left to follow the models.
            _thumbnails.Clear();

            // Stops further files from being probed. Probes already running still finish; see the
            // cancellation survey in sendfiles-popup-todo.md.
            _loadCancellation.Cancel();
        }

        // Enter never reaches CharacterReceived: XAML consumes it as a key before any character
        // is produced. It used to arrive as CR through CoreWindow, which sat below that pipeline.
        // Preview rather than KeyDown, so a focused ListViewItem cannot swallow it first.
        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key != VirtualKey.Enter || args.Handled)
            {
                return;
            }

            var focused = FocusManagerEx.TryGetFocusedElement(XamlRoot);
            if (focused is TextBox or RichEditBox or Button or MenuFlyoutItem)
            {
                return;
            }

            Accept();
            args.Handled = true;
        }

        private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
        {
            if (args.OriginalSource is TextBox or RichEditBox or Button or MenuFlyoutItem)
            {
                return;
            }

            var character = args.Character;
            if (character != '\u0016' && (char.IsControl(character) || char.IsWhiteSpace(character)))
            {
                return;
            }

            if (character == '\u0016' && CaptionInput.CanPasteClipboardContent)
            {
                CaptionInput.Focus(FocusState.Keyboard);
                CaptionInput.PasteFromClipboard();
            }
            else
            {
                CaptionInput.Focus(FocusState.Keyboard);
                CaptionInput.InsertText(character.ToString());
            }

            args.Handled = true;
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionPanel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                CaptionInput.InsertText(emoji.Value);
                CaptionInput.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                CaptionInput.InsertEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }

        private int ConvertCaptionRow(bool above)
        {
            ListAutocomplete.VerticalAlignment = above
                ? VerticalAlignment.Top
                : VerticalAlignment.Bottom;

            ListAutocomplete.Margin = new Thickness(0);
            ListAutocomplete.BorderThickness = new Thickness(1, above ? 0 : 1, 1, above ? 1 : 0);
            ListAutocomplete.CornerRadius = new CornerRadius(above ? 0 : 2, above ? 0 : 2, above ? 2 : 0, above ? 2 : 0);

            CaptionBorder.BorderThickness = new Thickness(0, 1, 0, above ? 1 : 0);

            return above ? 0 : 2;
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            if (StarCount > 0)
            {
                flyout.CreateFlyoutItem(MakeContentPaid, Strings.PaidMediaPriceButton, Icons.Coin);
            }
            else
            {
                if (_documentAllowed && IsMediaAllowed)
                {
                    var withCompressionText =
                        Items.All(x => x is StoragePhoto)
                        ? Items.Count != 1 ? Strings.SendAsPhotos : Strings.SendAsPhoto
                        : Items.All(x => x is StorageVideo) ? Items.Count != 1 ? Strings.SendAsVideo : Strings.SendAsVideos
                        : Strings.SendAsMedia;

                    flyout.CreateFlyoutItem(ToggleIsFilesSelected, false, withCompressionText, IsFilesSelected ? null : Icons.Checkmark, Windows.System.VirtualKey.P, Windows.System.VirtualKeyModifiers.Control);
                    flyout.CreateFlyoutItem(ToggleIsFilesSelected, true, Items.Count != 1 ? Strings.SendAsFiles : Strings.SendAsFile, IsFilesSelected ? Icons.Checkmark : null, Windows.System.VirtualKey.F, Windows.System.VirtualKeyModifiers.Control);
                }

                if (IsMediaSelected && Items.All(x => x is StoragePhoto or StorageVideo))
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(ToggleSendHighQuality, Strings.SendInHighQuality, SendHighQuality ? Icons.Checkmark : null);

                    flyout.CreateFlyoutSeparator();

                    flyout.CreateFlyoutItem(ToggleSendWithSpoiler, SendWithSpoiler ? Strings.DisablePhotoSpoiler : Strings.EnablePhotoSpoiler, Icons.SpoilerMedia);
                    flyout.CreateFlyoutItem(ToggleShowCaptionAboveMedia, ShowCaptionAboveMedia ? Strings.CaptionBelow : Strings.CaptionAbove, ShowCaptionAboveMedia ? Icons.MoveDown : Icons.MoveUp);

                    if (HasPaidMediaAllowed)
                    {
                        flyout.CreateFlyoutItem(MakeContentPaid, Strings.PaidMediaButton, Icons.Coin);
                    }
                }
            }

            flyout.ShowAt(sender as DependencyObject, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void ToggleIsFilesSelected(bool value)
        {
            // An explicit choice outranks the mode the caller asked for, even mid-probe.
            _mediaRequested = !value;

            IsFilesSelected = value;
            UpdateView();
            UpdatePanel();

            if (value)
            {
                ShowCaptionAboveMedia = false;
            }
        }

        private void ToggleShowCaptionAboveMedia()
        {
            ShowCaptionAboveMedia = !ShowCaptionAboveMedia;
        }

        private void ToggleSendHighQuality()
        {
            SendHighQuality = !SendHighQuality;
            AppSettings.SendLargePhotos = SendHighQuality;
        }

        private void ToggleSendWithSpoiler()
        {
            SendWithSpoiler = !SendWithSpoiler;
            UpdatePanel();
        }

        private async void MakeContentPaid()
        {
            var popup = new InputTeachingTip(InputPopupType.Stars);
            popup.Value = StarCount;
            popup.Maximum = ViewModel.ClientService.Options.PaidMediaMessageStarCountMax;

            popup.Title = Strings.PaidContentTitle;
            popup.Header = Strings.PaidContentPriceTitle;
            popup.ActionButtonContent = Strings.PaidContentUpdateButton;
            popup.ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
            popup.CloseButtonContent = Strings.Cancel;
            popup.PreferredPlacement = TeachingTipPlacementMode.Center;
            popup.IsLightDismissEnabled = true;
            popup.ShouldConstrainToRootBounds = true;

            //popup.Validating += (s, args) =>
            //{
            //    if (args.Value < ClientService.Options.StarWithdrawalCountMin)
            //    {
            //        ToastPopup.Show(Locale.Declension(Strings.R.BotStarsWithdrawMinLimit, ClientService.Options.StarWithdrawalCountMin), ToastPopupIcon.Info);
            //        args.Cancel = true;
            //    }
            //};

            var confirm = await popup.ShowAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            StarCount = popup.Value;
            _mediaRequested = true;
            IsFilesSelected = false;
            IsAlbum = true;

            UpdateView();
            UpdatePanel();
        }

        public bool CompareItems(StorageRow oldItem, StorageRow newItem)
        {
            if (oldItem is MosaicRow oldMosaic && newItem is MosaicRow newMosaic)
            {
                // Identity, not contents: comparing the media made a growing row a different item,
                // so every batch of arriving photos rebuilt it from nothing. UpdateItem carries
                // the new contents over instead.
                return oldMosaic.Index == newMosaic.Index;
            }
            else if (oldItem is FileRow oldFile && newItem is FileRow newFile)
            {
                return oldFile.Media.File?.Path == newFile.Media.File?.Path
                    && oldFile.Media.GetType() == newFile.Media.GetType()
                    && oldFile.AsDocument == newFile.AsDocument;
            }

            return false;
        }

        public void UpdateItem(StorageRow oldItem, StorageRow newItem)
        {
            // The collection keeps the old instance, so the new contents have to be moved onto it.
            if (oldItem is MosaicRow mosaic && newItem is MosaicRow updated)
            {
                mosaic.Update(updated.Media);

                var container = ScrollingHost.ContainerFromItem(mosaic) as SelectorItem;
                var content = container?.ContentTemplateRoot as Grid;

                // Nothing to do when it is not realized: OnContainerContentChanging reads the row
                // again on the way in.
                if (content != null && content.Children[0] is MosaicPanel panel)
                {
                    panel.UpdateMessage(mosaic);
                }
            }
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var files = await picker.PickMultipleFilesAsync(XamlRoot);
                if (files is { Count: > 0 })
                {
                    AppendFiles(files);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// A row of the popup's list — what gets drawn, which is not the same question as what gets
    /// sent. A <see cref="StorageAlbum"/> is one outgoing message; the two disagree in both
    /// directions, so the list holds these instead of holding send objects and pretending.
    /// </summary>
    public abstract partial class StorageRow
    {
    }

    /// <summary>
    /// Media drawn as a mosaic. Usually one album's worth, but not always: the grouping leaves a
    /// muted video standalone because it is sent on its own, and it is still a video.
    /// </summary>
    public sealed partial class MosaicRow : StorageRow
    {
        public MosaicRow(int index, IList<StorageMedia> media)
        {
            Index = index;
            Media = media;
        }

        /// <summary>
        /// Position among the rows, and this row's identity while diffing: one that gains a photo
        /// is the same row with new contents rather than a different row. Without that, every
        /// arriving photo tears its container down and builds it again, which costs a full
        /// remeasure and throws away the thumbnails the containers were holding.
        /// </summary>
        public int Index { get; }

        public IList<StorageMedia> Media { get; private set; }

        public void Update(IList<StorageMedia> media)
        {
            Media = media;
            Invalidate();
        }

        public const double ITEM_MARGIN = 2;
        public const double MAX_WIDTH = 420 + ITEM_MARGIN;
        public const double MAX_HEIGHT = 420 + ITEM_MARGIN;

        private ((Rect, MosaicItemPosition)[], Size)? _positions;

        public void Invalidate()
        {
            _positions = null;
        }

        public (Rect[], Size) GetPositionsForWidth(double w)
        {
            var positions = _positions ??= MosaicAlbumLayout.chatMessageBubbleMosaicLayout(MAX_WIDTH, MAX_HEIGHT, GetSizes());
            if (positions.Item1.Length == 1)
            {
                var size = new Size(Media[0].ActualWidth, Media[0].ActualHeight);
                var rect = new Rect(0, 0, size.Width, size.Height);

                positions = (new[] { (rect, MosaicItemPosition.None) }, size);
            }

            var ratioX = w / positions.Item2.Width;
            var ratioY = positions.Item2.Height * ratioX > MAX_HEIGHT ? MAX_HEIGHT / positions.Item2.Height : ratioX;

            var rects = new Rect[positions.Item1.Length];

            for (int i = 0; i < rects.Length; i++)
            {
                var rect = positions.Item1[i].Item1;
                var x = Sanitize(rect.X * ratioX);
                var y = Sanitize(rect.Y * ratioY);
                var width = Sanitize(rect.Width * ratioX);
                var height = Sanitize(rect.Height * ratioY);

                if (rects.Length == 1)
                {
                    height = Math.Clamp(height, 98, MAX_HEIGHT);
                }

                rects[i] = new Rect(x, y, width, height);
            }

            var finalWidth = Sanitize(positions.Item2.Width * ratioX);
            var finalHeight = Sanitize(positions.Item2.Height * ratioY);

            if (rects.Length == 1)
            {
                finalHeight = Math.Clamp(finalHeight, 98, MAX_HEIGHT);
            }

            return (rects, new Size(finalWidth, finalHeight));
        }

        private static double Sanitize(double value)
        {
            value = Math.Max(0, value);
            value = double.IsNaN(value) ? 0 : value;
            value = double.IsInfinity(value) ? 0 : value;

            return value;
        }

        private IEnumerable<Size> GetSizes()
        {
            foreach (var media in Media)
            {
                yield return new Size(media.ActualWidth, media.ActualHeight);
            }
        }
    }

    /// <summary>
    /// One item drawn as a named row. <see cref="AsDocument"/> is the files-mode glyph, which used
    /// to be carried by wrapping the item in a <see cref="StorageDocument"/> — a wrapper that then
    /// had to be unwrapped again to get back to the item the popup actually holds.
    /// </summary>
    public sealed partial class FileRow : StorageRow
    {
        public FileRow(StorageMedia media, bool asDocument)
        {
            Media = media;
            AsDocument = asDocument;
        }

        public StorageMedia Media { get; }

        public bool AsDocument { get; }
    }

    public partial class StorageMediaTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FileTemplate { get; set; }

        public DataTemplate AlbumTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                MosaicRow => AlbumTemplate,
                _ => FileTemplate
            };
        }
    }

    public sealed partial class MosaicPanel : Grid
    {
        private MosaicRow _row;

        public MosaicPanel()
        {
            // I don't like this much, but it's the easier way to add margins between children
            Margin = new Thickness(0, 0, -MosaicRow.ITEM_MARGIN, -MosaicRow.ITEM_MARGIN);
        }

        public IList<StorageMedia> Media => _row?.Media;

        private (Rect[], Size) _positions;

        public void Invalidate()
        {
            _positions = default;

            InvalidateMeasure();
            InvalidateArrange();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var row = _row;
            if (row == null || row.Media.Count < 1)
            {
                return base.MeasureOverride(availableSize);
            }

            var positions = row.GetPositionsForWidth(availableSize.Width);

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Measure(positions.Item1[i].ToSize());
            }

            _positions = positions;
            return positions.Item2;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var row = _row;
            if (row == null || row.Media.Count < 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            var positions = _positions;
            if (positions.Item1 == null || positions.Item1.Length < 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Arrange(positions.Item1[i]);
            }

            return finalSize;
        }

        public event EventHandler<StorageMedia> ItemClick;

        /// <summary>
        /// Children are reused across calls. An album gains photos as a drop is typed, and this
        /// runs again on every one of those: rebuilding it each time discards the templates and
        /// makes every surviving item re-request the thumbnail it already had.
        /// </summary>
        public void UpdateMessage(MosaicRow row)
        {
            _row = row;

            var media = row.Media;

            while (Children.Count > media.Count)
            {
                var index = Children.Count - 1;

                if (Children[index] is Button removed)
                {
                    removed.Click -= Element_Click;
                }

                Children.RemoveAt(index);
            }

            for (int i = 0; i < media.Count; i++)
            {
                if (i < Children.Count)
                {
                    // Content drives the template root's DataContext, which is what the popup
                    // hangs the thumbnail and the button visibility off.
                    if (Children[i] is Button existing && existing.Content != media[i])
                    {
                        existing.Content = media[i];
                    }

                    continue;
                }

                var element = new Button
                {
                    ContentTemplate = ItemTemplate,
                    Content = media[i],
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    MinWidth = 0,
                    MinHeight = 0,
                    MaxWidth = MosaicRow.MAX_WIDTH,
                    MaxHeight = MosaicRow.MAX_HEIGHT,
                    Margin = new Thickness(0, 0, MosaicRow.ITEM_MARGIN, MosaicRow.ITEM_MARGIN),
                    Padding = new Thickness(0),
                    Style = BootStrapper.Current.Resources["EmptyButtonStyle"] as Style
                };

                element.Click += Element_Click;

                Children.Add(element);
            }

            Invalidate();
        }

        private void Element_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button element && element.Content is StorageMedia item)
            {
                ItemClick?.Invoke(sender, item);
            }
        }

        public DataTemplate ItemTemplate
        {
            get { return (DataTemplate)GetValue(ItemTemplateProperty); }
            set { SetValue(ItemTemplateProperty, value); }
        }

        public static readonly DependencyProperty ItemTemplateProperty =
            DependencyProperty.Register("ItemTemplate", typeof(DataTemplate), typeof(MosaicPanel), new PropertyMetadata(null));
    }

    /// <summary>
    /// Owns the thumbnails shown by <see cref="SendFilesPopup"/>, for the lifetime of the popup.
    ///
    /// They used to hang off StorageMedia, which is the wrong owner twice over: the models are
    /// handed to the send loop and so outlive the popup, and nothing ever released a decoded
    /// bitmap once it had been produced. A photo costs roughly a megabyte and a video twice that,
    /// so a large drop retained tens of megabytes for the rest of the send.
    ///
    /// Entries are dropped as their album container is recycled, so the live set is bounded by
    /// what the ListView has realized rather than by how many files the user picked.
    ///
    /// A decode already under way cannot be stopped — neither BitmapImage.SetSourceAsync nor the
    /// video path takes a cancellation token — so closing the popup drops the result instead.
    /// </summary>
    public sealed partial class StorageThumbnailCache
    {
        // UI thread only: every entry point is a XAML callback or a continuation resumed on it.
        private readonly Dictionary<StorageMedia, ImageSource> _cache = new();
        private readonly Dictionary<StorageMedia, Task<ImageSource>> _inflight = new();

        public bool TryGet(StorageMedia media, out ImageSource source)
        {
            return _cache.TryGetValue(media, out source);
        }

        /// <summary>
        /// Returns null when there is no preview to show, and caches that so a file that cannot be
        /// decoded is not retried on every realization.
        /// </summary>
        public Task<ImageSource> GetAsync(StorageMedia media)
        {
            if (_cache.TryGetValue(media, out var cached))
            {
                return Task.FromResult(cached);
            }

            // Several containers can ask for the same file before the first decode returns, and
            // the album panel rebuilds all of its children on every UpdatePanel.
            if (_inflight.TryGetValue(media, out var pending))
            {
                return pending;
            }

            var task = DecodeAsync(media);
            _inflight[media] = task;

            return task;
        }

        private async Task<ImageSource> DecodeAsync(StorageMedia media)
        {
            ImageSource source = null;

            try
            {
                if (media.EditState is ImageGeneration editState && !editState.IsEmpty)
                {
                    try
                    {
                        // TODO: actual logical pixel size
                        source = await ImageHelper.CropAndPreviewAsync(media, editState, 600);
                    }
                    catch
                    {
                        // Fall back to the unedited preview below.
                    }
                }

                if (source == null)
                {
                    if (media is StorageVideo)
                    {
                        // TODO: actual logical pixel size
                        source = await ImageHelper.GetPreviewBitmapAsync(media, 600);
                    }
                    else
                    {
                        var preview = new BitmapImage
                        {
                            DecodePixelWidth = 300,
                            DecodePixelType = DecodePixelType.Logical
                        };

                        using var stream = await media.File.OpenReadAsync();
                        await preview.SetSourceAsync(stream);

                        source = preview;
                    }
                }
            }
            catch
            {
                // A file we cannot decode shows the type glyph instead.
                source = null;
            }

            // Release, Invalidate or Clear may have dropped this request while it was in flight.
            // Caching it now would either resurrect a bitmap nothing is left to release, or hand
            // back the pre-crop image.
            if (_inflight.Remove(media))
            {
                _cache[media] = source;
            }

            return source;
        }

        public void Invalidate(StorageMedia media)
        {
            _cache.Remove(media);
            _inflight.Remove(media);
        }

        public void Release(IEnumerable<StorageMedia> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var media in items)
            {
                _cache.Remove(media);
                _inflight.Remove(media);
            }
        }

        public void Clear()
        {
            _cache.Clear();
            _inflight.Clear();
        }
    }
}
