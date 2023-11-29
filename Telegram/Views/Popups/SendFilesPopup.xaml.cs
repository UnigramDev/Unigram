//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Entities;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Views.Popups
{
    public sealed partial class SendFilesPopup : ContentPopup, IViewWithAutocomplete, INotifyPropertyChanged
    {
        public ComposeViewModel ViewModel { get; private set; }
        public MvxObservableCollection<StorageMedia> Items { get; private set; }

        private ICollection _autocomplete;
        public ICollection Autocomplete
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

        private bool _mediaAllowed;
        private bool _documentAllowed;

        public bool IsMediaOnly => _mediaAllowed && _documentAllowed && Items.All(x => x is StoragePhoto or StorageVideo);
        public bool IsAlbumAvailable
        {
            get
            {
                if (IsMediaSelected)
                {
                    return Items.Count > 1 && Items.Count <= 10 && Items.All(x => (x is StoragePhoto || x is StorageVideo) && x.Ttl == null);
                }

                return Items.Count is > 1 and <= 10;
            }
        }
        public bool IsTtlAvailable { get; }

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

        public string SendWithoutCompression
        {
            get
            {
                if (IsMediaSelected)
                {
                    return Items.Count == 1 ? Strings.SendAsFile : Strings.SendAsFiles;
                }

                return string.Format(Strings.SendItems, Locale.Declension(Strings.R.Files, Items.Count));
            }
        }

        private bool _wasAlbum = true;

        private bool _isAlbum = true;
        public bool IsAlbum
        {
            get => _isAlbum;
            set
            {
                if (_isAlbum != value)
                {
                    _isAlbum = IsAlbumAvailable && value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAlbum)));
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

        public bool Spoiler { get; private set; }
        public bool? Schedule { get; private set; }
        public bool? Silent { get; private set; }

        public SendFilesPopup(ComposeViewModel viewModel, IEnumerable<StorageMedia> items, bool media, bool mediaAllowed, bool documentAllowed, bool ttl, bool schedule, bool savedMessages)
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Send;
            SecondaryButtonText = Strings.Cancel;

            IsTtlAvailable = ttl;
            IsSavedMessages = savedMessages;
            CanSchedule = schedule;

            _mediaAllowed = mediaAllowed;
            _documentAllowed = documentAllowed;

            DataContext = viewModel;
            ViewModel = viewModel;

            Items = new MvxObservableCollection<StorageMedia>(items);
            Items.CollectionChanged += OnCollectionChanged;
            IsMediaSelected = media && mediaAllowed && Items.All(x => x is StoragePhoto or StorageVideo);
            IsFilesSelected = !IsMediaSelected;

            EmojiPanel.DataContext = EmojiDrawerViewModel.GetForCurrentView(viewModel.SessionId);
            CaptionInput.CustomEmoji = CustomEmoji;
            CaptionInput.ViewModel = viewModel;

            UpdateView();
            UpdatePanel();
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SendWithoutCompression)));

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

            CaptionInput.Document.GetText(TextGetOptions.None, out string text);

            var query = text.Substring(0, Math.Min(CaptionInput.Document.Selection.EndPosition, text.Length));
            var entity = AutocompleteEntityFinder.Search(query, out string result, out int index);

            if (e.ClickedItem is User user && entity == AutocompleteEntity.Username)
            {
                // TODO: find username
                var adjust = 0;
                var username = user.ActiveUsername(result);

                string insert;
                if (string.IsNullOrEmpty(username))
                {
                    insert = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                    adjust = 1;
                }
                else
                {
                    insert = username;
                }

                var range = CaptionInput.Document.GetRange(CaptionInput.Document.Selection.StartPosition - result.Length - adjust, CaptionInput.Document.Selection.StartPosition);
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
                    range.SetText(TextSetOptions.None, string.Empty);

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

            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            if (args.Item is User user)
            {
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

                photo.SetUser(ViewModel.ClientService, user, 32);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var storage = args.Item as StorageMedia;
            if (storage == null)
            {
                return;
            }

            var root = args.ItemContainer.ContentTemplateRoot as Grid;
            if (root == null)
            {
                return;
            }

            if (root is AspectView aspect)
            {
                aspect.Constraint = new Size(storage.Width, storage.Height);
            }

            var glyph = root.FindName("Glyph") as TextBlock;
            glyph.Text = storage is StoragePhoto
                ? Icons.Image
                : storage is StorageVideo
                ? Icons.Play
                : Icons.Document;

            var title = root.FindName("Title") as TextBlock;
            var subtitle = root.FindName("Subtitle") as TextBlock;

            if (title == null || subtitle == null)
            {
                return;
            }

            title.Text = storage.File.Name;
            subtitle.Text = FileSizeConverter.Convert((long)storage.Size);
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
            var overlay = root.FindName("Overlay") as Border;

            var mute = root.FindName("Mute") as ToggleButton;
            var crop = root.FindName("Crop") as ToggleButton;
            var ttl = root.FindName("Ttl") as ToggleButton;

            overlay.Visibility = storage is StorageVideo ? Visibility.Visible : Visibility.Collapsed;

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

        public void Accept()
        {
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
            var content = Clipboard.GetContent();
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

        public async Task HandlePackageAsync(DataPackageView package)
        {
            try
            {
                if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
                {
                    var bitmap = await package.GetBitmapAsync();

                    var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.png", DateTime.Now);
                    var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                    using (var source = await bitmap.OpenReadAsync())
                    using (var destination = await cache.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await RandomAccessStream.CopyAsync(
                            source.GetInputStreamAt(0),
                            destination.GetOutputStreamAt(0));
                    }

                    var photo = await StoragePhoto.CreateAsync(cache);
                    if (photo != null)
                    {
                        Items.Add(photo);

                        UpdateView();
                        UpdatePanel();
                    }
                }
                else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await package.GetStorageItemsAsync();
                    var results = await StorageMedia.CreateAsync(items);

                    foreach (var item in results)
                    {
                        Items.Add(item);
                    }

                    UpdateView();
                    UpdatePanel();
                }
            }
            catch { }
        }

        private int _itemsState = -1;
        private int _panelState = -1;

        private void UpdateView()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMediaOnly)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAlbumAvailable)));

            if (IsMediaSelected && !IsMediaOnly && !_mediaAllowed)
            {
                IsMediaSelected = false;
                IsFilesSelected = true;
            }
        }

        private void UpdatePanel()
        {
            if (IsAlbum && !IsAlbumAvailable)
            {
                IsAlbum = false;
            }
            else if (_wasAlbum && IsAlbumAvailable)
            {
                IsAlbum = true;
            }

            var state = IsAlbum && IsAlbumAvailable && IsMediaSelected ? 1 : 0;
            if (state != _panelState)
            {
                _panelState = state;
                if (state == 1)
                {
                    FindName(nameof(AlbumPanel));
                    AlbumPanel.Visibility = Visibility.Visible;

                    if (ListPanel != null)
                    {
                        ListPanel.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    if (Album != null)
                    {
                        AlbumPanel.Visibility = Visibility.Collapsed;
                    }

                    FindName(nameof(ListPanel));
                    ListPanel.Visibility = Visibility.Visible;
                }
            }

            if (Album?.ItemsPanelRoot is SendFilesAlbumPanel panel && IsAlbum)
            {
                var layout = new List<Size>();

                foreach (var item in Items)
                {
                    layout.Add(new Size(item.Width, item.Height));
                }

                panel.Sizes = layout;
                panel.Invalidate();
            }

            var mediaState = IsMediaSelected ? 1 : 0;
            if (mediaState != _itemsState && ScrollingHost != null)
            {
                _itemsState = mediaState;
                ScrollingHost.ItemTemplate = Resources[mediaState == 1 ? "MediaItemTemplate" : "FileItemTemplate"] as DataTemplate;
            }
        }

        private void PivotRadioButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateView();
            UpdatePanel();
        }

        private void Album_Click(object sender, RoutedEventArgs e)
        {
            _wasAlbum = AlbumButton.IsChecked == true;
            IsAlbum = _wasAlbum;

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
                Window.Current.ShowToast(sender as FrameworkElement,
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
                    new LocalFileSource(ttl == null
                        ? "ms-appx:///Assets/Toasts/AutoRemoveOff.tgs"
                        : "ms-appx:///Assets/Toasts/AutoRemoveOn.tgs"),
                    TeachingTipPlacementMode.TopLeft);
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
            }
        }

        private async void Crop_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as ToggleButton;
            if (button.Tag is StorageMedia media)
            {
                var dialog = new EditMediaPopup(media);

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    media.Refresh();
                }
            }
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Tag is StorageMedia media)
            {
                Items.Remove(media);
            }
        }

        protected override void OnApplyTemplate()
        {
            IsPrimaryButtonSplit = CanSchedule;

            var button = GetTemplateChild("PrimarySplitButton") as Button;
            if (button != null && CanSchedule)
            {
                button.Click += PrimaryButton_ContextRequested;
            }

            base.OnApplyTemplate();
        }

        private void PrimaryButton_ContextRequested(object sender, RoutedEventArgs args)
        {
            var self = IsSavedMessages;

            var flyout = new MenuFlyout();

            var media = Items.All(x => x is StoragePhoto or StorageVideo);
            if (media && !IsFilesSelected)
            {
                flyout.CreateFlyoutItem(() => Spoiler = !Spoiler, Spoiler ? Strings.DisablePhotoSpoiler : Strings.EnablePhotoSpoiler, Icons.TabInPrivate);
                flyout.CreateFlyoutSeparator();
            }

            flyout.CreateFlyoutItem(() => { Silent = true; Hide(ContentDialogResult.Primary); }, Strings.SendWithoutSound, Icons.AlertOff);
            flyout.CreateFlyoutItem(() => { Schedule = true; Hide(ContentDialogResult.Primary); }, self ? Strings.SetReminder : Strings.ScheduleMessage, Icons.CalendarClock);

            flyout.ShowAt(sender as DependencyObject, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CaptionInput.Focus(FocusState.Keyboard);
            Window.Current.CoreWindow.CharacterReceived += OnCharacterReceived;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.CoreWindow.CharacterReceived -= OnCharacterReceived;
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            var character = Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0)
            {
                return;
            }
            else if (character != "\u0016" && character != "\r" && char.IsControl(character[0]))
            {
                return;
            }
            else if (character != "\u0016" && character != "\r" && char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManager.GetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox and not Button and not MenuFlyoutItem))
            {
                var popups = VisualTreeHelper.GetOpenPopups(Window.Current);

                foreach (var popup in popups)
                {
                    if (popup.Child is not SendFilesPopup and not Rectangle)
                    {
                        return;
                    }
                }

                if (character == "\u0016" && CaptionInput.CanPasteClipboardContent)
                {
                    CaptionInput.Focus(FocusState.Keyboard);
                    CaptionInput.PasteFromClipboard();
                }
                else if (character == "\r")
                {
                    Accept();
                }
                else
                {
                    CaptionInput.Focus(FocusState.Keyboard);
                    CaptionInput.InsertText(character);
                }

                args.Handled = true;
            }
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(CaptionInput, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                EmojiFlyout.Hide();

                CaptionInput.InsertText(emoji.Value);
                CaptionInput.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                EmojiFlyout.Hide();

                CaptionInput.InsertEmoji(sticker);
                CaptionInput.Focus(FocusState.Programmatic);
            }
        }
    }

    public class SendFilesAlbumPanel : Grid
    {
        public const double ITEM_MARGIN = 2;
        public const double MAX_WIDTH = 320 + ITEM_MARGIN;
        public const double MAX_HEIGHT = 420 + ITEM_MARGIN;

        private (Rect[], Size) _positions;
        private ((Rect, MosaicItemPosition)[], Size)? _positionsBase;

        public List<Size> Sizes;

        protected override Size MeasureOverride(Size availableSize)
        {
            var sizes = Sizes;
            if (sizes == null || sizes.Count == 1)
            {
                return base.MeasureOverride(availableSize);
            }

            var positions = GetPositionsForWidth(availableSize.Width);

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Measure(new Size(positions.Item1[i].Width, positions.Item1[i].Height));
            }

            _positions = positions;
            return positions.Item2;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var positions = _positions;
            if (positions.Item1 == null || positions.Item1.Length == 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            for (int i = 0; i < Math.Min(positions.Item1.Length, Children.Count); i++)
            {
                Children[i].Arrange(positions.Item1[i]);
            }

            return positions.Item2;
        }

        public void Invalidate()
        {
            _positionsBase = null;

            InvalidateMeasure();
            InvalidateArrange();
        }

        private (Rect[], Size) GetPositionsForWidth(double w)
        {
            var positions = _positionsBase ??= MosaicAlbumLayout.chatMessageBubbleMosaicLayout(new Size(MAX_WIDTH, MAX_HEIGHT), Sizes);

            var ratio = w / MAX_WIDTH;
            var rects = new Rect[positions.Item1.Length];

            for (int i = 0; i < rects.Length; i++)
            {
                var rect = positions.Item1[i].Item1;

                var width = Math.Max(0, rect.Width * ratio);
                var height = Math.Max(0, rect.Height * ratio);

                width = double.IsNaN(width) ? 0 : width;
                height = double.IsNaN(height) ? 0 : height;

                rects[i] = new Rect(rect.X * ratio, rect.Y * ratio, width, height);
            }

            var finalWidth = Math.Max(0, positions.Item2.Width * ratio);
            var finalHeight = Math.Max(0, positions.Item2.Height * ratio);

            finalWidth = double.IsNaN(finalWidth) ? 0 : finalWidth;
            finalHeight = double.IsNaN(finalHeight) ? 0 : finalHeight;

            return (rects, new Size(finalWidth, finalHeight));
        }
    }
}
