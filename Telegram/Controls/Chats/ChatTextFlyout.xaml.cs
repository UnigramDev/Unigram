//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatTextFlyout : GridEx
    {
        private readonly FormattedTextBox _textBox;

        private readonly ZoomableListHandler _zoomer;
        private readonly AnimatedListHandler _handler;

        public ViewModelBase ViewModel => _textBox.DataContext as ViewModelBase;

        public ChatTextFlyout(FormattedTextBox textBox, AutocompleteCollection itemsSource)
        {
            InitializeComponent();

            // TODO: this might need to change depending on context
            _handler = new AnimatedListHandler(ScrollingHost, AnimatedListType.Stickers);

            _zoomer = new ZoomableListHandler(ScrollingHost);
            _zoomer.Opening = _handler.Suspend;
            _zoomer.Closing = _handler.Resume;

            _textBox = textBox;
            ScrollingHost.ItemsSource = itemsSource;

            MinWidth = 40;
            MaxWidth = 184;
            MinHeight = 40;
        }

        public ListViewBase ControlledList => ScrollingHost;

        public IAutocompleteCollection ItemsSource
        {
            get
            {
                if (ScrollingHost.ItemsSource is AutocompleteCollection collection)
                {
                    return collection.Source;
                }

                return null;
            }
        }

        public void Update(IAutocompleteCollection source)
        {
            if (ScrollingHost.ItemsSource is AutocompleteCollection collection)
            {
                collection.Replace(source);
            }
        }

        protected override void OnUnloaded()
        {
            _handler.UnloadItems();
            _zoomer.Release();
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextGridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;

                _zoomer.ElementPrepared(args.ItemContainer);
            }

            if (args.Item is EmojiData or Sticker)
            {
                args.ItemContainer.Margin = new Thickness(4, 4, 0, 4);
                args.ItemContainer.CornerRadius = new CornerRadius(4, 4, 4, 4);
            }
            else
            {
                args.ItemContainer.Margin = new Thickness();
                args.ItemContainer.CornerRadius = new CornerRadius();
            }

            args.ItemContainer.MinHeight = 0;

            args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is Sticker sticker)
            {
                var content = args.ItemContainer.ContentTemplateRoot as Grid;

                var animated = content.Children[0] as AnimatedImage;
                animated.Source = DelayedFileSource.FromSticker(ViewModel?.ClientService, sticker);

                AutomationProperties.SetName(args.ItemContainer, sticker.Emoji);
            }
            else if (args.Item is EmojiData emoji)
            {
                AutomationProperties.SetName(args.ItemContainer, emoji.Value);
            }

            args.Handled = true;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var selection = _textBox.Document.Selection.GetClone();
            var entity = AutocompleteEntityFinder.Search(selection, out string result, out int index);

            void InsertText(string insert)
            {
                var range = _textBox.Document.GetRange(index, _textBox.Document.Selection.StartPosition);
                range.SetText(TextSetOptions.None, insert);

                _textBox.Document.Selection.StartPosition = index + insert.Length;
            }

            if (e.ClickedItem is EmojiData emoji)
            {
                SettingsService.Current.Emoji.AddRecentEmoji(emoji);
                InsertText($"{emoji.Value}");
            }
            else if (e.ClickedItem is Sticker sticker && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                SettingsService.Current.Emoji.AddRecentEmoji(sticker.Emoji, customEmoji.CustomEmojiId);

                var range = _textBox.Document.GetRange(index, _textBox.Document.Selection.StartPosition);

                _textBox.InsertEmoji(range, sticker.Emoji, customEmoji.CustomEmojiId);
                _textBox.Document.Selection.StartPosition = range.EndPosition + 1;

                var precedingRange = _textBox.Document.GetRange(index, index);
                var offset = index;

                // Let's see if the current emoji is preceded by the same emoji and replace all the occurrences
                while (AutocompleteEntityFinder.TrySearch(precedingRange, out AutocompleteEntity precedingEntity, out string precedingResult, out int precedingIndex))
                {
                    if (precedingEntity != entity || precedingResult != result)
                    {
                        break;
                    }

                    precedingRange = _textBox.Document.GetRange(precedingIndex, offset);
                    _textBox.InsertEmoji(precedingRange, sticker.Emoji, customEmoji.CustomEmojiId);

                    precedingRange = _textBox.Document.GetRange(precedingIndex, precedingIndex);
                    offset = precedingIndex;
                }
            }
        }
    }
}
