using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Td.Api;
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
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class SendFilesView : ContentDialog, IViewWithAutocomplete, INotifyPropertyChanged
    {
        public DialogViewModel ViewModel { get; set; }
        public MvxObservableCollection<StorageMedia> Items { get; private set; }

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

        private FormattedText _caption;
        public FormattedText Caption
        {
            get { return _caption; }
            set
            {
                _caption = value;
                CaptionInput.SetText(value);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public SendFilesView()
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Resources.Send;
            SecondaryButtonText = Strings.Resources.Cancel;

            Items = new MvxObservableCollection<StorageMedia>();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Caption = CaptionInput.GetFormattedText();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
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

            Autocomplete = null;
        }

        private void Autocomplete_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var height = e.NewSize.Height;
            var padding = ListAutocomplete.ActualHeight - Math.Min(154, ListAutocomplete.Items.Count * 44);

            //ListAutocomplete.Padding = new Thickness(0, padding, 0, 0);
            //AutocompleteHeader.Margin = new Thickness(0, padding, 0, -height);
            //AutocompleteHeader.Height = height;

            Debug.WriteLine("Autocomplete size changed");
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

                name.Text = user.GetFullName();
                username.Text = string.IsNullOrEmpty(user.Username) ? string.Empty : $" @{user.Username}";

                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }
        }

        private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
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

            var title = root.FindName("Title") as TextBlock;
            var subtitle = root.FindName("Subtitle") as TextBlock;

            if (title == null || subtitle == null)
            {
                return;
            }

            var props = await storage.File.GetBasicPropertiesAsync();

            title.Text = storage.File.Name;
            subtitle.Text = FileSizeConverter.Convert((int)props.Size);
        }

        public void Accept()
        {
            throw new NotImplementedException();
        }

        private async void OnPaste(object sender, TextControlPasteEventArgs e)
        {
            e.Handled = true;
            await HandlePackageAsync(Clipboard.GetContent());
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
            var boh = string.Join(", ", package.AvailableFormats);

            if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
            {
                var bitmap = await package.GetBitmapAsync();
                var media = new ObservableCollection<StorageMedia>();

                var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.png", DateTime.Now);
                var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                using (var stream = await bitmap.OpenReadAsync())
                {
                    var result = await ImageHelper.TranscodeAsync(stream, cache, BitmapEncoder.PngEncoderId);
                    if (result != null)
                    {
                        Items.Add(new StorageDocument(result));
                    }

                    return;
                }
            }
            else if (package.AvailableFormats.Contains(StandardDataFormats.StorageItems))
            {
                var items = await package.GetStorageItemsAsync();
                var files = new List<StorageFile>(items.Count);

                foreach (StorageFile file in items.OfType<StorageFile>())
                {
                    files.Add(file);
                }

                foreach (var item in files)
                {
                    Items.Add(new StorageDocument(item));
                }
            }
        }
    }
}
