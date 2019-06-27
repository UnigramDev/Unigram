using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Views
{
    public sealed partial class SendFilesView : TLContentDialog, IViewWithAutocomplete, INotifyPropertyChanged
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

#if DEBUG
        public bool IsMediaOnly => Items.All(x => x is StoragePhoto || x is StorageVideo);
        public bool IsAlbumAvailable => IsMediaSelected && Items.All(x => (x is StoragePhoto || x is StorageVideo) && x.Ttl == 0);
#else
        public bool IsMediaOnly => false;
        public bool IsAlbumAvailable => false;
#endif

        private bool _isMediaSelected;
        public bool IsMediaSelected
        {
            get { return _isMediaSelected; }
            set
            {
                if (_isMediaSelected != value)
                {
                    _isMediaSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsMediaSelected"));
                }
            }
        }

        private bool _isFilesSelected;
        public bool IsFilesSelected
        {
            get { return _isFilesSelected; }
            set
            {
                if (_isFilesSelected != value)
                {
                    _isFilesSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsFilesSelected"));
                }
            }
        }

        private bool _isAlbum;
        public bool IsAlbum
        {
            get { return _isAlbum; }
            set
            {
                if (_isAlbum != value)
                {
                    _isAlbum = IsMediaOnly ? value : false;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsAlbum"));
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

        public SendFilesView(IEnumerable<StorageMedia> items, bool media)
        {
            InitializeComponent();

            PrimaryButtonText = Strings.Resources.Send;
            SecondaryButtonText = Strings.Resources.Cancel;

            Items = new MvxObservableCollection<StorageMedia>(items);
            IsMediaSelected = media && IsMediaOnly;
            IsFilesSelected = !IsMediaSelected;

            UpdateView();
            UpdatePanel();
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

            var glyph = root.FindName("Glyph") as TextBlock;
            glyph.Text = storage is StoragePhoto
                ? "\uEB9F"
                : storage is StorageVideo
                ? "\uE768"
                : "\uE160";

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
            if (package.AvailableFormats.Contains(StandardDataFormats.Bitmap))
            {
                var bitmap = await package.GetBitmapAsync();

                var fileName = string.Format("image_{0:yyyy}-{0:MM}-{0:dd}_{0:HH}-{0:mm}-{0:ss}.png", DateTime.Now);
                var cache = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                using (var stream = await bitmap.OpenReadAsync())
                {
                    var result = await ImageHelper.TranscodeAsync(stream, cache, BitmapEncoder.PngEncoderId);
                    var photo = await StoragePhoto.CreateAsync(result, true);
                    if (photo == null)
                    {
                        return;
                    }

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

        private int _itemsState = -1;
        private int _panelState = -1;

        private void UpdateView()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsMediaOnly"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsAlbumAvailable"));

            if (IsMediaSelected && !IsMediaOnly)
            {
                IsMediaSelected = false;
                IsFilesSelected = true;
            }

            var state = IsMediaSelected ? 1 : 0;
            if (state != _itemsState)
            {
                List.ItemTemplate = Resources[state == 1 ? "MediaItemTemplate" : "FileItemTemplate"] as DataTemplate;
            }

            _itemsState = state;
        }

        private void UpdatePanel()
        {
#if DEBUG
            if (IsAlbum && !IsAlbumAvailable)
            {
                IsAlbum = false;
            }

            var state = IsAlbum && IsAlbumAvailable && IsMediaSelected ? 1 : 0;
            if (state != _panelState)
            {
                List.ItemsPanel = Resources[state == 1 ? "AlbumPanelTemplate" : "FilesPanelTemplate"] as ItemsPanelTemplate;
                //List.ItemContainerStyle = Resources[state == 1 ? "AlbumContainerStyle" : "FilesContainerStyle"] as Style;
            }

            _panelState = state;

            if (List.ItemsPanelRoot is SendFilesAlbumPanel panel)
            {
                var layout = new GroupedMedia();

                foreach (var item in Items)
                {
                    layout.Messages.Add(item);
                }

                layout.Calculate();

                panel.Layout = layout;
                panel.InvalidateMeasure();
                panel.InvalidateArrange();
            }
#endif
        }

        private void PivotRadioButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateView();
            UpdatePanel();
        }

        private void SendFilesAlbumPanel_Loading(FrameworkElement sender, object args)
        {
            UpdatePanel();
        }

        private async void TTLSeconds_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Tag is StorageMedia media)
            {
                Hide();

                var dialog = new MessageTtlView(media.IsPhoto);
                dialog.Value = media.Ttl > 0 ? media.Ttl : ViewModel.Settings.LastMessageTtl;

                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    if (dialog.Value > 0)
                    {
                        ViewModel.Settings.LastMessageTtl = dialog.Value;
                    }

                    media.Ttl = dialog.Value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsGroupingEnabled"));
                }

                await ShowAsync();
            }
        }

        private void Crop_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button.Tag is StorageMedia media)
            {
                Items.Remove(media);
            }
        }
    }

    public class SendFilesAlbumPanel : Grid
    {
        public const double ITEM_MARGIN = 2;
        public const double MAX_WIDTH = 320 + ITEM_MARGIN;
        public const double MAX_HEIGHT = 420 + ITEM_MARGIN;

        public GroupedMedia Layout { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var groupedMessages = Layout;
            if (groupedMessages == null || groupedMessages.Messages.Count == 1)
            {
                return base.MeasureOverride(availableSize);
            }

            var positions = groupedMessages.Positions.ToList();

            var groupedWidth = (double)groupedMessages.Width;
            var width = groupedMessages.Width / 800d * Math.Min(availableSize.Width, MAX_WIDTH);
            //var width = availableSize.Width;
            var height = width / MAX_WIDTH * MAX_HEIGHT;

            var size = new Size(width, groupedMessages.Height * height);

            for (int i = 0; i < Math.Min(positions.Count, Children.Count); i++)
            {
                Children[i].Measure(new Size(positions[i].Value.Width / groupedWidth * width, height * positions[i].Value.Height));
            }

            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var groupedMessages = Layout;
            if (groupedMessages == null || groupedMessages.Messages.Count == 1)
            {
                return base.ArrangeOverride(finalSize);
            }

            var positions = groupedMessages.Positions.ToList();

            var groupedWidth = (double)groupedMessages.Width;
            var width = finalSize.Width;
            var height = width / MAX_WIDTH * MAX_HEIGHT;

            var total = 0d;

            for (int i = 0; i < Math.Min(positions.Count, Children.Count); i++)
            {
                var position = positions[i];

                var top = total;
                var left = 0d;

                if (i > 0)
                {
                    var pos = positions[i - 1];
                    // in one row
                    if (pos.Value.MinY == position.Value.MinY)
                    {
                        for (var j = i - 1; j >= 0; j--)
                        {
                            pos = positions[j];
                            if (pos.Value.MinY == position.Value.MinY)
                            {
                                left += pos.Value.Width / groupedWidth * width;
                            }
                        }
                    }
                    // in one column
                    else if (position.Value.SpanSize == groupedMessages.MaxSizeWidth || position.Value.SpanSize == 1000)
                    {
                        left = position.Value.LeftSpanOffset / groupedWidth * width;
                        // find common big message
                        KeyValuePair<StorageMedia, GroupedMessagePosition>? leftColumn = null;
                        for (var j = i - 1; j >= 0; j--)
                        {
                            pos = positions[j];
                            if (pos.Value.SiblingHeights != null)
                            {
                                leftColumn = pos;
                                break;
                            }
                            else
                            {
                                top += height * pos.Value.Height;
                            }
                        }
                    }
                    else
                    {
                        top = total += height * positions[i - 1].Value.Height;
                    }
                }

                Children[i].Arrange(new Rect(left, top, positions[i].Value.Width / groupedWidth * width, height * positions[i].Value.Height));
            }

            return finalSize;
        }
    }

    public class GroupedMedia
    {
        public const int POSITION_FLAG_LEFT = 1;
        public const int POSITION_FLAG_RIGHT = 2;
        public const int POSITION_FLAG_TOP = 4;
        public const int POSITION_FLAG_BOTTOM = 8;

        private int _maxSizeWidth = 800;
        private List<GroupedMessagePosition> _posArray = new List<GroupedMessagePosition>();

        public int MaxSizeWidth => _maxSizeWidth;

        public long GroupedId { get; set; }
        public bool HasSibling { get; private set; }

        public float Height { get; private set; }
        public int Width { get; private set; }

        public UniqueList<string, StorageMedia> Messages { get; } = new UniqueList<string, StorageMedia>(x => x.File.Path);
        public Dictionary<StorageMedia, GroupedMessagePosition> Positions { get; } = new Dictionary<StorageMedia, GroupedMessagePosition>();

        private class MessageGroupedLayoutAttempt
        {
            public float[] Heights { get; private set; }
            public int[] LineCounts { get; private set; }

            public MessageGroupedLayoutAttempt(int i1, int i2, float f1, float f2)
            {
                LineCounts = new int[] { i1, i2 };
                Heights = new float[] { f1, f2 };
            }

            public MessageGroupedLayoutAttempt(int i1, int i2, int i3, float f1, float f2, float f3)
            {
                LineCounts = new int[] { i1, i2, i3 };
                Heights = new float[] { f1, f2, f3 };
            }

            public MessageGroupedLayoutAttempt(int i1, int i2, int i3, int i4, float f1, float f2, float f3, float f4)
            {
                LineCounts = new int[] { i1, i2, i3, i4 };
                Heights = new float[] { f1, f2, f3, f4 };
            }
        }

        private float MultiHeight(float[] array, int start, int end)
        {
            float sum = 0.0f;
            for (int a = start; a < end; a++)
            {
                sum += array[a];
            }

            return 800.0f / sum;
        }

        public void Calculate()
        {
            _posArray.Clear();
            Positions.Clear();
            int count = Messages.Count;
            if (count <= 1)
            {
                return;
            }

            int totalWidth = 0;
            float totalHeight = 0.0f;

            int firstSpanAdditionalSize = 200;
            float maxSizeHeight = 814.0f;
            StringBuilder proportions = new StringBuilder();
            float averageAspectRatio = 1.0f;
            bool isOut = false;
            int maxX = 0;
            bool forceCalc = false;

            for (int a = 0; a < count; a++)
            {
                StorageMedia messageObject = Messages[a];
                IList<PhotoSize> photoThumbs = null;
                if (a == 0)
                {
                    //isOut = messageObject.isOutOwner();
                }
                int w = (int)messageObject.Width;
                int h = (int)messageObject.Height;

                GroupedMessagePosition position = new GroupedMessagePosition();
                position.IsLast = a == count - 1;
                position.AspectRatio = w / (float)h;

                if (position.AspectRatio > 1.2f)
                {
                    proportions.Append("w");
                }
                else if (position.AspectRatio < 0.8f)
                {
                    proportions.Append("n");
                }
                else
                {
                    proportions.Append("q");
                }

                averageAspectRatio += position.AspectRatio;

                if (position.AspectRatio > 2.0f)
                {
                    forceCalc = true;
                }

                Positions[messageObject] = position;
                _posArray.Add(position);
            }

            //int minHeight = AndroidUtilities.dp(120);
            //int minWidth = (int)(AndroidUtilities.dp(120) / (Math.Min(AndroidUtilities.displaySize.x, AndroidUtilities.displaySize.y) / (float)_maxSizeWidth));
            //int paddingsWidth = (int)(AndroidUtilities.dp(40) / (Math.Min(AndroidUtilities.displaySize.x, AndroidUtilities.displaySize.y) / (float)_maxSizeWidth));
            int minHeight = 120;
            int minWidth = 96;
            int paddingsWidth = 32;

            float maxAspectRatio = _maxSizeWidth / maxSizeHeight;
            averageAspectRatio = averageAspectRatio / count;

            if (!forceCalc && (count == 2 || count == 3 || count == 4))
            {
                if (count == 2)
                {
                    GroupedMessagePosition position1 = _posArray[0];
                    GroupedMessagePosition position2 = _posArray[1];
                    String pString = proportions.ToString();
                    if (pString.Equals("ww") && averageAspectRatio > 1.4 * maxAspectRatio && position1.AspectRatio - position2.AspectRatio < 0.2)
                    {
                        float height = (float)Math.Round(Math.Min(_maxSizeWidth / position1.AspectRatio, Math.Min(_maxSizeWidth / position2.AspectRatio, maxSizeHeight / 2.0f))) / maxSizeHeight;
                        position1.Set(0, 0, 0, 0, _maxSizeWidth, height, POSITION_FLAG_LEFT | POSITION_FLAG_RIGHT | POSITION_FLAG_TOP);
                        position2.Set(0, 0, 1, 1, _maxSizeWidth, height, POSITION_FLAG_LEFT | POSITION_FLAG_RIGHT | POSITION_FLAG_BOTTOM);

                        totalWidth = _maxSizeWidth;
                        totalHeight = height * 2;
                    }
                    else if (pString.Equals("ww") || pString.Equals("qq"))
                    {
                        int width = _maxSizeWidth / 2;
                        float height = (float)Math.Round(Math.Min(width / position1.AspectRatio, Math.Min(width / position2.AspectRatio, maxSizeHeight))) / maxSizeHeight;
                        position1.Set(0, 0, 0, 0, width, height, POSITION_FLAG_LEFT | POSITION_FLAG_BOTTOM | POSITION_FLAG_TOP);
                        position2.Set(1, 1, 0, 0, width, height, POSITION_FLAG_RIGHT | POSITION_FLAG_BOTTOM | POSITION_FLAG_TOP);
                        maxX = 1;

                        totalWidth = width + width;
                        totalHeight = height;
                    }
                    else
                    {
                        int secondWidth = (int)Math.Max(0.4f * _maxSizeWidth, Math.Round((_maxSizeWidth / position1.AspectRatio / (1.0f / position1.AspectRatio + 1.0f / position2.AspectRatio))));
                        int firstWidth = _maxSizeWidth - secondWidth;
                        if (firstWidth < minWidth)
                        {
                            int diff = minWidth - firstWidth;
                            firstWidth = minWidth;
                            secondWidth -= diff;
                        }

                        float height = (float)Math.Min(maxSizeHeight, Math.Round(Math.Min(firstWidth / position1.AspectRatio, secondWidth / position2.AspectRatio))) / maxSizeHeight;
                        position1.Set(0, 0, 0, 0, firstWidth, height, POSITION_FLAG_LEFT | POSITION_FLAG_BOTTOM | POSITION_FLAG_TOP);
                        position2.Set(1, 1, 0, 0, secondWidth, height, POSITION_FLAG_RIGHT | POSITION_FLAG_BOTTOM | POSITION_FLAG_TOP);
                        maxX = 1;

                        totalWidth = firstWidth + secondWidth;
                        totalHeight = height;
                    }
                }
                else if (count == 3)
                {
                    GroupedMessagePosition position1 = _posArray[0];
                    GroupedMessagePosition position2 = _posArray[1];
                    GroupedMessagePosition position3 = _posArray[2];
                    if (proportions[0] == 'n')
                    {
                        float thirdHeight = (float)Math.Min(maxSizeHeight * 0.5f, Math.Round(position2.AspectRatio * _maxSizeWidth / (position3.AspectRatio + position2.AspectRatio)));
                        float secondHeight = maxSizeHeight - thirdHeight;
                        int rightWidth = (int)Math.Max(minWidth, Math.Min(_maxSizeWidth * 0.5f, Math.Round(Math.Min(thirdHeight * position3.AspectRatio, secondHeight * position2.AspectRatio))));

                        int leftWidth = (int)Math.Round(Math.Min(maxSizeHeight * position1.AspectRatio + paddingsWidth, _maxSizeWidth - rightWidth));
                        position1.Set(0, 0, 0, 1, leftWidth, 1.0f, POSITION_FLAG_LEFT | POSITION_FLAG_BOTTOM | POSITION_FLAG_TOP);

                        position2.Set(1, 1, 0, 0, rightWidth, secondHeight / maxSizeHeight, POSITION_FLAG_RIGHT | POSITION_FLAG_TOP);

                        position3.Set(0, 1, 1, 1, rightWidth, thirdHeight / maxSizeHeight, POSITION_FLAG_RIGHT | POSITION_FLAG_BOTTOM);
                        position3.SpanSize = _maxSizeWidth;

                        position1.SiblingHeights = new float[] { thirdHeight / maxSizeHeight, secondHeight / maxSizeHeight };

                        if (isOut)
                        {
                            position1.SpanSize = _maxSizeWidth - rightWidth;
                        }
                        else
                        {
                            position2.SpanSize = _maxSizeWidth - leftWidth;
                            position3.LeftSpanOffset = leftWidth;
                        }
                        HasSibling = true;
                        maxX = 1;

                        totalWidth = leftWidth + rightWidth;
                        totalHeight = 1.0f;
                    }
                    else
                    {
                        float firstHeight = (float)Math.Round(Math.Min(_maxSizeWidth / position1.AspectRatio, (maxSizeHeight) * 0.66f)) / maxSizeHeight;
                        position1.Set(0, 1, 0, 0, _maxSizeWidth, firstHeight, POSITION_FLAG_LEFT | POSITION_FLAG_RIGHT | POSITION_FLAG_TOP);

                        int width = _maxSizeWidth / 2;
                        float secondHeight = (float)Math.Min(maxSizeHeight - firstHeight, Math.Round(Math.Min(width / position2.AspectRatio, width / position3.AspectRatio))) / maxSizeHeight;
                        position2.Set(0, 0, 1, 1, width, secondHeight, POSITION_FLAG_LEFT | POSITION_FLAG_BOTTOM);
                        position3.Set(1, 1, 1, 1, width, secondHeight, POSITION_FLAG_RIGHT | POSITION_FLAG_BOTTOM);
                        maxX = 1;

                        totalWidth = _maxSizeWidth;
                        totalHeight = firstHeight + secondHeight;
                    }
                }
                else if (count == 4)
                {
                    GroupedMessagePosition position1 = _posArray[0];
                    GroupedMessagePosition position2 = _posArray[1];
                    GroupedMessagePosition position3 = _posArray[2];
                    GroupedMessagePosition position4 = _posArray[3];
                    if (proportions[0] == 'w')
                    {
                        float h0 = (float)Math.Round(Math.Min(_maxSizeWidth / position1.AspectRatio, maxSizeHeight * 0.66f)) / maxSizeHeight;
                        position1.Set(0, 2, 0, 0, _maxSizeWidth, h0, POSITION_FLAG_LEFT | POSITION_FLAG_RIGHT | POSITION_FLAG_TOP);

                        float h = (float)Math.Round(_maxSizeWidth / (position2.AspectRatio + position3.AspectRatio + position4.AspectRatio));
                        int w0 = (int)Math.Max(minWidth, Math.Min(_maxSizeWidth * 0.4f, h * position2.AspectRatio));
                        int w2 = (int)Math.Max(Math.Max(minWidth, _maxSizeWidth * 0.33f), h * position4.AspectRatio);
                        int w1 = _maxSizeWidth - w0 - w2;
                        h = Math.Min(maxSizeHeight - h0, h);
                        h /= maxSizeHeight;
                        position2.Set(0, 0, 1, 1, w0, h, POSITION_FLAG_LEFT | POSITION_FLAG_BOTTOM);
                        position3.Set(1, 1, 1, 1, w1, h, POSITION_FLAG_BOTTOM);
                        position4.Set(2, 2, 1, 1, w2, h, POSITION_FLAG_RIGHT | POSITION_FLAG_BOTTOM);
                        maxX = 2;

                        totalWidth = _maxSizeWidth;
                        totalHeight = h0 + h;
                    }
                    else
                    {
                        int w = (int)Math.Max(minWidth, Math.Round(maxSizeHeight / (1.0f / position2.AspectRatio + 1.0f / position3.AspectRatio + 1.0f / _posArray[3].AspectRatio)));
                        float h0 = Math.Min(0.33f, Math.Max(minHeight, w / position2.AspectRatio) / maxSizeHeight);
                        float h1 = Math.Min(0.33f, Math.Max(minHeight, w / position3.AspectRatio) / maxSizeHeight);
                        float h2 = 1.0f - h0 - h1;
                        int w0 = (int)Math.Round(Math.Min(maxSizeHeight * position1.AspectRatio + paddingsWidth, _maxSizeWidth - w));

                        position1.Set(0, 0, 0, 2, w0, h0 + h1 + h2, POSITION_FLAG_LEFT | POSITION_FLAG_TOP | POSITION_FLAG_BOTTOM);

                        position2.Set(1, 1, 0, 0, w, h0, POSITION_FLAG_RIGHT | POSITION_FLAG_TOP);

                        position3.Set(0, 1, 1, 1, w, h1, POSITION_FLAG_RIGHT);
                        position3.SpanSize = _maxSizeWidth;

                        position4.Set(0, 1, 2, 2, w, h2, POSITION_FLAG_RIGHT | POSITION_FLAG_BOTTOM);
                        position4.SpanSize = _maxSizeWidth;

                        if (isOut)
                        {
                            position1.SpanSize = _maxSizeWidth - w;
                        }
                        else
                        {
                            position2.SpanSize = _maxSizeWidth - w0;
                            position3.LeftSpanOffset = w0;
                            position4.LeftSpanOffset = w0;
                        }
                        position1.SiblingHeights = new float[] { h0, h1, h2 };
                        HasSibling = true;
                        maxX = 1;

                        totalWidth = w + w0;
                        totalHeight = h0 + h1 + h2;
                    }
                }
            }
            else
            {
                float[] croppedRatios = new float[_posArray.Count];
                for (int a = 0; a < count; a++)
                {
                    if (averageAspectRatio > 1.1f)
                    {
                        croppedRatios[a] = Math.Max(1.0f, _posArray[a].AspectRatio);
                    }
                    else
                    {
                        croppedRatios[a] = Math.Min(1.0f, _posArray[a].AspectRatio);
                    }
                    croppedRatios[a] = Math.Max(0.66667f, Math.Min(1.7f, croppedRatios[a]));
                }

                int firstLine;
                int secondLine;
                int thirdLine;
                int fourthLine;
                List<MessageGroupedLayoutAttempt> attempts = new List<MessageGroupedLayoutAttempt>();
                for (firstLine = 1; firstLine < croppedRatios.Length; firstLine++)
                {
                    secondLine = croppedRatios.Length - firstLine;
                    if (firstLine > 3 || secondLine > 3)
                    {
                        continue;
                    }
                    attempts.Add(new MessageGroupedLayoutAttempt(firstLine, secondLine, MultiHeight(croppedRatios, 0, firstLine), MultiHeight(croppedRatios, firstLine, croppedRatios.Length)));
                }

                for (firstLine = 1; firstLine < croppedRatios.Length - 1; firstLine++)
                {
                    for (secondLine = 1; secondLine < croppedRatios.Length - firstLine; secondLine++)
                    {
                        thirdLine = croppedRatios.Length - firstLine - secondLine;
                        if (firstLine > 3 || secondLine > (averageAspectRatio < 0.85f ? 4 : 3) || thirdLine > 3)
                        {
                            continue;
                        }
                        attempts.Add(new MessageGroupedLayoutAttempt(firstLine, secondLine, thirdLine, MultiHeight(croppedRatios, 0, firstLine), MultiHeight(croppedRatios, firstLine, firstLine + secondLine), MultiHeight(croppedRatios, firstLine + secondLine, croppedRatios.Length)));
                    }
                }

                for (firstLine = 1; firstLine < croppedRatios.Length - 2; firstLine++)
                {
                    for (secondLine = 1; secondLine < croppedRatios.Length - firstLine; secondLine++)
                    {
                        for (thirdLine = 1; thirdLine < croppedRatios.Length - firstLine - secondLine; thirdLine++)
                        {
                            fourthLine = croppedRatios.Length - firstLine - secondLine - thirdLine;
                            if (firstLine > 3 || secondLine > 3 || thirdLine > 3 || fourthLine > 3)
                            {
                                continue;
                            }
                            attempts.Add(new MessageGroupedLayoutAttempt(firstLine, secondLine, thirdLine, fourthLine, MultiHeight(croppedRatios, 0, firstLine), MultiHeight(croppedRatios, firstLine, firstLine + secondLine), MultiHeight(croppedRatios, firstLine + secondLine, firstLine + secondLine + thirdLine), MultiHeight(croppedRatios, firstLine + secondLine + thirdLine, croppedRatios.Length)));
                        }
                    }
                }

                MessageGroupedLayoutAttempt optimal = null;
                float optimalDiff = 0.0f;
                float maxHeight = _maxSizeWidth / 3 * 4;
                for (int a = 0; a < attempts.Count; a++)
                {
                    MessageGroupedLayoutAttempt attempt = attempts[a];
                    float height = 0;
                    float minLineHeight = float.MaxValue;
                    for (int b = 0; b < attempt.Heights.Length; b++)
                    {
                        height += attempt.Heights[b];
                        if (attempt.Heights[b] < minLineHeight)
                        {
                            minLineHeight = attempt.Heights[b];
                        }
                    }

                    float diff = Math.Abs(height - maxHeight);
                    if (attempt.LineCounts.Length > 1)
                    {
                        if (attempt.LineCounts[0] > attempt.LineCounts[1] || (attempt.LineCounts.Length > 2 && attempt.LineCounts[1] > attempt.LineCounts[2]) || (attempt.LineCounts.Length > 3 && attempt.LineCounts[2] > attempt.LineCounts[3]))
                        {
                            diff *= 1.5f;
                        }
                    }

                    if (minLineHeight < minWidth)
                    {
                        diff *= 1.5f;
                    }

                    if (optimal == null || diff < optimalDiff)
                    {
                        optimal = attempt;
                        optimalDiff = diff;
                    }
                }
                if (optimal == null)
                {
                    return;
                }

                int index = 0;
                float y = 0.0f;

                for (int i = 0; i < optimal.LineCounts.Length; i++)
                {
                    int c = optimal.LineCounts[i];
                    float lineHeight = optimal.Heights[i];
                    int spanLeft = _maxSizeWidth;
                    GroupedMessagePosition posToFix = null;
                    maxX = Math.Max(maxX, c - 1);
                    for (int k = 0; k < c; k++)
                    {
                        float ratio = croppedRatios[index];
                        int width = (int)(ratio * lineHeight);
                        spanLeft -= width;
                        GroupedMessagePosition pos = _posArray[index];
                        int flags = 0;
                        if (i == 0)
                        {
                            flags |= POSITION_FLAG_TOP;
                        }
                        if (i == optimal.LineCounts.Length - 1)
                        {
                            flags |= POSITION_FLAG_BOTTOM;
                        }
                        if (k == 0)
                        {
                            flags |= POSITION_FLAG_LEFT;
                            if (isOut)
                            {
                                posToFix = pos;
                            }
                        }
                        if (k == c - 1)
                        {
                            flags |= POSITION_FLAG_RIGHT;
                            if (!isOut)
                            {
                                posToFix = pos;
                            }
                        }
                        pos.Set(k, k, i, i, width, lineHeight / maxSizeHeight, flags);
                        index++;
                    }
                    posToFix.Width += spanLeft;
                    posToFix.SpanSize += spanLeft;
                    y += lineHeight;
                }

                totalWidth = _maxSizeWidth;
                totalHeight = y / maxSizeHeight;
            }
            for (int a = 0; a < count; a++)
            {
                GroupedMessagePosition pos = _posArray[a];
                if (isOut)
                {
                    if (pos.MinX == 0)
                    {
                        pos.SpanSize += firstSpanAdditionalSize;
                    }
                    if ((pos.Flags & POSITION_FLAG_RIGHT) != 0)
                    {
                        pos.IsEdge = true;
                    }
                }
                else
                {
                    if (pos.MaxX == maxX || (pos.Flags & POSITION_FLAG_RIGHT) != 0)
                    {
                        pos.SpanSize += firstSpanAdditionalSize;
                    }
                    if ((pos.Flags & POSITION_FLAG_LEFT) != 0)
                    {
                        pos.IsEdge = true;
                    }
                }
            }

            Width = totalWidth;
            Height = totalHeight;
        }
    }

    public class GlyphTtlButton : GlyphButton
    {
        public int Ttl
        {
            get { return (int)GetValue(TtlProperty); }
            set { SetValue(TtlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Ttl.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TtlProperty =
            DependencyProperty.Register("Ttl", typeof(int), typeof(GlyphTtlButton), new PropertyMetadata(0, OnTtlChanged));

        private static void OnTtlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlyphTtlButton)d).OnTtlChanged((int)e.NewValue, (int)e.OldValue);
        }

        private void OnTtlChanged(int newValue, int oldValue)
        {
            VisualStateManager.GoToState(this, newValue == 0 ? "Unselected" : "Selected", false);

            // TODO: WRONG!!!
            if (newValue == 0)
            {
                ClearValue(Button.ForegroundProperty);
            }
            else
            {
                Foreground = App.Current.Resources["SystemControlForegroundAccentBrush"] as SolidColorBrush;
            }
        }
    }
}
