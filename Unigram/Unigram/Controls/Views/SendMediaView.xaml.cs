using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls.Chats;
using Unigram.Converters;
using Unigram.Entities;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class SendMediaView : OverlayPage, INotifyPropertyChanged
    {
        public DialogViewModel ViewModel { get; set; }

        public MvxObservableCollection<StorageMedia> Items { get; } = new MvxObservableCollection<StorageMedia>();
        public MvxObservableCollection<StorageMedia> SelectedItems { get; } = new MvxObservableCollection<StorageMedia>();

        private StorageMedia _selectedItem;
        public StorageMedia SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                if (_selectedItem != null && value != null)
                {
                    value.Caption = CaptionInput.GetFormattedText(ViewModel.ProtoService)
                        .Substring(0, ViewModel.CacheService.Options.MessageCaptionLengthMax);
                }

                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
                }

                if (_selectedItem != null)
                {
                    CaptionInput.SetText(_selectedItem.Caption
                        .Substring(0, ViewModel.CacheService.Options.MessageCaptionLengthMax));
                }
            }
        }

        public int SelectedIndex
        {
            get
            {
                var item = SelectedItem;
                if (item == null)
                {
                    return 0;
                }

                return SelectedItems.IndexOf(item) + 1;
            }
        }

        private bool _isTtlEnabled;
        public bool IsTTLEnabled
        {
            get
            {
                return _isTtlEnabled;
            }
            set
            {
                if (_isTtlEnabled != value)
                {
                    _isTtlEnabled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsTTLEnabled"));
                }
            }
        }

        public bool IsGroupingEnabled
        {
            get
            {
                return SelectedItems.Count > 1 && !SelectedItems.Any(x => x.Ttl > 0);
            }
        }

        private bool _isGrouped;
        public bool IsGrouped
        {
            get
            {
                return _isGrouped;
            }
            set
            {
                if (_isGrouped != value)
                {
                    _isGrouped = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGrouped"));
                }
            }
        }

        private ICollection _autocomplete;
        public ICollection Autocomplete
        {
            get
            {
                return _autocomplete;
            }
            set
            {
                if (_autocomplete != value)
                {
                    _autocomplete = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Autocomplete"));
                }
            }
        }

        #region Editing

        public bool IsEditing
        {
            get
            {
                return _isEditingCompression || _isEditingCropping;
            }
        }

        private bool _isEditingCompression;
        public bool IsEditingCompression
        {
            get
            {
                return _isEditingCompression;
            }
            set
            {
                if (_isEditingCompression != value)
                {
                    _isEditingCompression = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditingCompression"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditing"));
                }
            }
        }

        private bool _isEditingCropping;
        public bool IsEditingCropping
        {
            get
            {
                return _isEditingCropping;
            }
            set
            {
                if (_isEditingCropping != value)
                {
                    _isEditingCropping = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditingCropping"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditing"));
                }
            }
        }

        #endregion

        #region Binding

        private string ConvertAccept(bool editing)
        {
            return editing ? "\uE10B" : "\uE725";
        }

        private string ConvertCompression(StorageMedia media, double compression)
        {
            var value = (int)compression;
            if (media is StorageVideo video)
            {
                return video.ToString(value);
            }

            return null;
        }

        private string ConvertGrouped(bool grouped)
        {
            return grouped ? Strings.Resources.GroupPhotosHelp : Strings.Resources.SinglePhotosHelp;
        }

        private bool ConvertSelected(StorageMedia media)
        {
            return SelectedItems.Contains(media);
        }

        #endregion

        public SendMediaView()
        {
            InitializeComponent();
            DataContext = this;

            //var seconds = new int[29];
            //for (int i = 0; i < seconds.Length; i++)
            //{
            //    seconds[i] = i;
            //}

            //TTLSeconds.ItemsSource = seconds;

            ProportionsBox.SelectionChanged += (s, args) =>
            {
                Cropper.Proportions = (ImageCroppingProportions)ProportionsBox.SelectedItem;
            };

            TTLSeconds.RegisterPropertyChangedCallback(GlyphButton.GlyphProperty, OnSecondsChanged);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override void OnBackRequestedOverride(object sender, HandledEventArgs e)
        {
            if (IsEditingCompression)
            {
                e.Handled = true;
                IsEditingCompression = false;
            }
            else if (IsEditingCropping)
            {
                e.Handled = true;
                IsEditingCropping = false;
            }
            else
            {
                e.Handled = true;
                Hide(ContentDialogResult.None);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

            IsGrouped = ViewModel.Settings.IsSendGrouped;

            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.RichEditBox", "MaxLength"))
            {
                CaptionInput.MaxLength = ViewModel.CacheService.Options.MessageCaptionLengthMax;
            }

            if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                CaptionInput.Focus(FocusState.Keyboard);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            KeyboardPlaceholder.Height = new GridLength(args.OccludedRect.Height);
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            KeyboardPlaceholder.Height = new GridLength(1, GridUnitType.Auto);
        }

        public void Accept()
        {
            Accept_Click(null, null);
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (Items == null)
            {
                Hide(ContentDialogResult.Secondary);
                return;
            }

            if (IsEditingCompression && SelectedItem is StorageVideo video)
            {
                video.Compression = (int)CompressionValue.Value;

                IsEditingCompression = false;
                return;
            }

            if (IsEditingCropping && SelectedItem is StorageMedia media)
            {
                media.CropRectangle = Cropper.CropRectangle;
                media.Refresh();

                Select_Click(null, null);

                IsEditingCropping = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
                return;
            }

            if (SelectedItem != null && SelectedItems.IsEmpty())
            {
                SelectedItems.Add(SelectedItem);
            }

            if (IsGroupingEnabled)
            {
                ViewModel.Settings.IsSendGrouped = IsGrouped;
            }

            SelectedItem.Caption = CaptionInput.GetFormattedText(ViewModel.ProtoService)
                .Substring(0, ViewModel.CacheService.Options.MessageCaptionLengthMax);

            Hide(ContentDialogResult.Primary);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (IsEditingCompression && SelectedItem is StorageVideo video)
            {
                IsEditingCompression = false;
                return;
            }

            if (IsEditingCropping && SelectedItem is StorageMedia media)
            {
                IsEditingCropping = false;
                return;
            }

            Hide(ContentDialogResult.Secondary);
        }

        private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
                {
                    Accept_Click(null, null);
                }

                Flip.Focus(FocusState.Programmatic);
                e.Handled = true;
            }
        }

        private async void More_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.AddRange(Constants.MediaTypes);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null)
            {
                foreach (var file in files)
                {
                    var storage = await StorageMedia.CreateAsync(file, true);
                    if (storage != null)
                    {
                        Items.Add(storage);
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void TTLSeconds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //VisualStateManager.GoToState(TTLSeconds, TTLSeconds.SelectedIndex == 0 ? "Unselected" : "Selected", false);
        }

        private void OnSecondsChanged(DependencyObject sender, DependencyProperty dp)
        {
            VisualStateManager.GoToState(TTLSeconds, SelectedItem.Ttl == 0 ? "Unselected" : "Selected", false);
            //VisualStateManager.GoToState(this, SelectedItem.TTLSeconds == null ? "Unselected" : "Selected", false);

            // TODO: WRONG!!!
            if (SelectedItem.Ttl == 0)
            {
                TTLSeconds.ClearValue(Button.ForegroundProperty);
            }
            else
            {
                TTLSeconds.Foreground = LayoutRoot.Resources["SystemControlForegroundAccentBrush"] as SolidColorBrush;
            }
        }

        private async void TTLSeconds_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MessageTtlView(SelectedItem.IsPhoto);
            dialog.Value = SelectedItem.Ttl > 0 ? SelectedItem.Ttl : ViewModel.Settings.LastMessageTtl;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                if (dialog.Value > 0)
                {
                    ViewModel.Settings.LastMessageTtl = dialog.Value;
                }

                SelectedItem.Ttl = dialog.Value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGroupingEnabled"));
            }
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            CaptionInput.Document.GetText(TextGetOptions.None, out string hidden);
            CaptionInput.Document.GetText(TextGetOptions.NoHidden, out string text);

            if (e.ClickedItem is User user && ChatTextBox.SearchByUsername(text.Substring(0, Math.Min(CaptionInput.Document.Selection.EndPosition, text.Length)), out string username, out int index))
            {
                var insert = string.Empty;
                var adjust = 0;

                if (string.IsNullOrEmpty(user.Username))
                {
                    insert = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                    adjust = 1;
                }
                else
                {
                    insert = user.Username;
                }

                var range = CaptionInput.Document.GetRange(CaptionInput.Document.Selection.StartPosition - username.Length - adjust, CaptionInput.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                if (string.IsNullOrEmpty(user.Username))
                {
                    range.Link = $"\"tg-user://{user.Id}\"";
                }

                CaptionInput.Document.GetRange(range.EndPosition, range.EndPosition).SetText(TextSetOptions.None, " ");
                CaptionInput.Document.Selection.StartPosition = range.EndPosition + 1;
            }
            else if (e.ClickedItem is EmojiSuggestion emoji && ChatTextBox.SearchByEmoji(text.Substring(0, Math.Min(CaptionInput.Document.Selection.EndPosition, text.Length)), out string replacement))
            {
                var insert = $"{emoji.Emoji} ";
                var start = CaptionInput.Document.Selection.StartPosition - 1 - replacement.Length + insert.Length;
                var range = CaptionInput.Document.GetRange(CaptionInput.Document.Selection.StartPosition - 1 - replacement.Length, CaptionInput.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                //TextField.Document.GetRange(start, start).SetText(TextSetOptions.None, " ");
                //TextField.Document.Selection.StartPosition = start + 1;
                CaptionInput.Document.Selection.StartPosition = start;
            }

            ViewModel.Autocomplete = null;
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var height = e.NewSize.Height;
            var padding = ListAutocomplete.ActualHeight - Math.Min(154, ListAutocomplete.Items.Count * 44);

            //ListAutocomplete.Padding = new Thickness(0, padding, 0, 0);
            AutocompleteHeader.Margin = new Thickness(0, padding, 0, -height);
            AutocompleteHeader.Height = height;

            Debug.WriteLine("Autocomplete size changed");
        }

        private void Compress_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageVideo video)
            {
                IsEditingCompression = true;
                CompressionValue.Maximum = video.MaxCompression - 1;
                CompressionValue.Value = video.Compression;
            }
        }

        private void Crop_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageMedia media)
            {
                IsEditingCropping = true;

                if (media.Bitmap is SoftwareBitmapSource source)
                {
                    var width = media.Width;
                    var height = media.Height;

                    if (width > 1280 || height > 1280)
                    {
                        double ratioX = (double)1280 / width;
                        double ratioY = (double)1280 / height;
                        double ratio = Math.Min(ratioX, ratioY);

                        width = (uint)(width * ratio);
                        height = (uint)(height * ratio);
                    }

                    //var container = Flip.ContainerFromItem(Flip.SelectedItem) as SelectorItem;
                    //if (container != null)
                    //{
                    //    var content = container.ContentTemplateRoot as Border;
                    //    var zoom = content.Child as Viewbox;

                    //    ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("Crop", zoom);
                    //}

                    Cropper.SetSource(media.File, source, width, height);
                    Cropper.Proportions = media.CropProportions;
                    Cropper.CropRectangle = media.CropRectangle ?? Rect.Empty;

                    ProportionsBox.ItemsSource = ImageCropper.GetProportionsFor(width, height);
                    ProportionsBox.SelectedItem = media.CropProportions;
                }
            }
        }

        private void Proportions_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageMedia media)
            {
                if (media.CropProportions == ImageCroppingProportions.Custom)
                {
                    var width = media.Width;
                    var height = media.Height;

                    if (width > 1280 || height > 1280)
                    {
                        double ratioX = (double)1280 / width;
                        double ratioY = (double)1280 / height;
                        double ratio = Math.Min(ratioX, ratioY);

                        width = (uint)(width * ratio);
                        height = (uint)(height * ratio);
                    }

                    var flyout = new MenuFlyout();
                    var items = ImageCropper.GetProportionsFor(width, height);

                    var handler = new RoutedEventHandler((s, args) =>
                    {
                        if (s is MenuFlyoutItem option)
                        {
                            media.CropProportions = (ImageCroppingProportions)option.Tag;
                            Cropper.Proportions = media.CropProportions;
                        }
                    });

                    foreach (var item in items)
                    {
                        var option = new MenuFlyoutItem();
                        option.Click += handler;
                        option.Text = ProportionsToLabelConverter.Convert(item);
                        option.Tag = item;
                        option.MinWidth = 140;
                        option.HorizontalContentAlignment = HorizontalAlignment.Center;

                        flyout.Items.Add(option);
                    }

                    if (flyout.Items.Count > 0)
                    {
                        flyout.ShowAt((Button)sender);
                    }
                }
                else
                {
                    media.CropProportions = ImageCroppingProportions.Custom;
                    Cropper.Proportions = ImageCroppingProportions.Custom;
                }
            }
        }

        private void ResetCrop_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageMedia media)
            {
                media.CropProportions = ImageCroppingProportions.Custom;
                Cropper.Reset(ImageCroppingProportions.Custom);
                Cropper.Proportions = ImageCroppingProportions.Custom;
            }
        }

        private Visibility ConvertProportions(ImageCroppingProportions proportions, bool positive)
        {
            if (positive)
            {
                return proportions == ImageCroppingProportions.Custom ? Visibility.Collapsed : Visibility.Visible;
            }

            return proportions == ImageCroppingProportions.Custom ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetItems(ObservableCollection<StorageMedia> storages)
        {
            Items.ReplaceWith(storages);
            SelectedItems.ReplaceWith(storages.Where(x => x.IsSelected));
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            var item = SelectedItem;
            if (item == null)
            {
                return;
            }

            if (SelectedItems.Contains(item))
            {
                SelectedItems.Remove(item);
            }
            else
            {
                SelectedItems.Add(item);
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGroupingEnabled"));
        }

        private void Select_Tapped(object sender, TappedRoutedEventArgs e)
        {
            Select_Click(null, null);
        }

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            var package = Clipboard.GetContent();
            if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
            {
                e.Handled = true;

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

                    var photo = await StoragePhoto.CreateAsync(cache, true) as StorageMedia;
                    if (photo == null)
                    {
                        return;
                    }

                    media.Add(photo);
                }

                if (package.AvailableFormats.Contains(StandardDataFormats.Text))
                {
                    media[0].Caption = new FormattedText(await package.GetTextAsync(), new TextEntity[0]);
                }

                foreach (var item in media)
                {
                    SelectedItems.Add(item);
                    Items.Add(item);
                }

                SelectedItem = media[0];
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGroupingEnabled"));
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = true;

                var items = await package.GetStorageItemsAsync();
                var media = new ObservableCollection<StorageMedia>();
                var files = new List<StorageFile>(items.Count);

                foreach (StorageFile file in items)
                {
                    if (file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/bmp", StringComparison.OrdinalIgnoreCase) ||
                        file.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
                    {
                        var photo = await StoragePhoto.CreateAsync(file, true);
                        if (photo != null)
                        {
                            media.Add(photo);
                        }
                    }
                    else if (file.ContentType == "video/mp4")
                    {
                        var video = await StorageVideo.CreateAsync(file, true);
                        if (video != null)
                        {
                            media.Add(video);
                        }
                    }

                    files.Add(file);
                }

                // Send compressed __only__ if user is dropping photos and videos only
                if (media.Count > 0 && media.Count == files.Count)
                {
                    foreach (var item in media)
                    {
                        SelectedItems.Add(item);
                        Items.Add(item);
                    }

                    SelectedItem = media[0];
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGroupingEnabled"));
                }
                else if (files.Count > 0)
                {
                    // Not supported here!
                }
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
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

                name.Text = user.GetFullName();
                username.Text = string.IsNullOrEmpty(user.Username) ? string.Empty : $" @{user.Username}";

                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }
        }
    }

    public class HeaderFlipView : FlipView
    {
        #region Content

        public object Content
        {
            get { return (object)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(object), typeof(HeaderFlipView), new PropertyMetadata(null));

        #endregion
    }
}
