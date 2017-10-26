using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Common;
using Unigram.Core.Models;
using Unigram.Models;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Views
{
    public sealed partial class SendMediaView : ContentDialogBase, INotifyPropertyChanged
    {
        public DialogViewModel ViewModel { get; set; }

        public ObservableCollection<StorageMedia> Items { get; set; }

        private StorageMedia _selectedItem;
        public StorageMedia SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
                }

                if (_selectedItem != null)
                {
                    CaptionInput.Text = _selectedItem.Caption ?? string.Empty;
                }
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

            CroppoBox.SelectionChanged += (s, args) =>
            {
                Cropper.Proportions = (ImageCroppingProportions)CroppoBox.SelectedItem;
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
                Hide(ContentDialogBaseResult.None);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;

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

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (Items == null)
            {
                Hide(ContentDialogBaseResult.Cancel);
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

                IsEditingCropping = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
                return;
            }

            if (SelectedItem != null && Items.All(x => x.IsSelected == false))
            {
                SelectedItem.IsSelected = true;
            }

            Hide(ContentDialogBaseResult.OK);
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

            Hide(ContentDialogBaseResult.Cancel);
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
            VisualStateManager.GoToState(TTLSeconds, SelectedItem.TTLSeconds == null ? "Unselected" : "Selected", false);
            //VisualStateManager.GoToState(this, SelectedItem.TTLSeconds == null ? "Unselected" : "Selected", false);

            // TODO: WRONG!!!
            if (SelectedItem.TTLSeconds == null)
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
            var dialog = new SelectTTLSecondsView();
            dialog.TTLSeconds = SelectedItem.TTLSeconds;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                SelectedItem.TTLSeconds = dialog.TTLSeconds;
            }
        }

        private void Autocomplete_ItemClick(object sender, ItemClickEventArgs e)
        {
            var text = CaptionInput.Text.ToString();

            if (e.ClickedItem is TLUser user && BubbleTextBox.SearchByUsername(text.Substring(0, Math.Min(CaptionInput.SelectionStart, text.Length)), out string username))
            {
                var insert = $"@{user.Username} ";
                var start = CaptionInput.SelectionStart - 1 - username.Length;
                var part1 = text.Substring(0, start);
                var part2 = text.Substring(start + 1 + username.Length);

                CaptionInput.Text = part1 + insert + part2;
                CaptionInput.SelectionStart = start + insert.Length;

                Autocomplete = null;
            }
            else if (e.ClickedItem is EmojiSuggestion emoji && BubbleTextBox.SearchByEmoji(text.Substring(0, Math.Min(CaptionInput.SelectionStart, text.Length)), out string replacement))
            {
                var insert = $"{emoji.Emoji} ";
                var start = CaptionInput.SelectionStart - 1 - replacement.Length;
                var part1 = text.Substring(0, start);
                var part2 = text.Substring(start + 1 + replacement.Length);

                CaptionInput.Text = part1 + insert + part2;
                CaptionInput.SelectionStart = start + insert.Length;

                Autocomplete = null;
            }
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

        private async void Crop_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is StorageMedia media)
            {
                IsEditingCropping = true;

                if (media.Bitmap is SoftwareBitmapSource source)
                {
                    var props = await media.File.Properties.GetImagePropertiesAsync();
                    var width = props.Width;
                    var height = props.Height;

                    if (width > 1280 || height > 1280)
                    {
                        double ratioX = (double)1280 / width;
                        double ratioY = (double)1280 / height;
                        double ratio = Math.Min(ratioX, ratioY);

                        width = (uint)(width * ratio);
                        height = (uint)(height * ratio);
                    }

                    Cropper.SetSource(media.File, source, width, height);
                    Cropper.Proportions = ImageCroppingProportions.Custom;
                    Cropper.CropRectangle = media.CropRectangle ?? Rect.Empty;

                    CroppoBox.ItemsSource = ImageCropper.GetProportionsFor(width, height);
                    CroppoBox.SelectedItem = Cropper.Proportions;
                }
            }
        }

        private void ResetCrop_Click(object sender, RoutedEventArgs e)
        {
            Cropper.Reset();
        }
    }
}
