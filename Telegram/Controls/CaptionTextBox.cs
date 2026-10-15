//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    /// <summary>
    /// A caption's box over RichEdit: the emoji and custom emoji offered as they are typed, in a
    /// flyout of its own. Mentions are <see cref="CaptionController"/>'s, which works on either box.
    /// </summary>
    public partial class CaptionTextBox : FormattedTextBox
    {
        public ViewModelBase ViewModel { get; set; }

        public long ChatId => ViewModel is ComposeViewModel compose ? compose.Chat?.Id ?? 0 : 0;

        public CaptionTextBox()
        {
            SelectionChanged += OnSelectionChanged;
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space && Document.Selection.Length == 0)
            {
                try
                {
                    var clone = Document.Selection.GetClone();
                    if (clone.EndPosition > Document.Selection.EndPosition && AreTheSame(clone.CharacterFormat, Document.Selection.CharacterFormat))
                    {

                    }
                    else
                    {
                        Document.Selection.CharacterFormat = Document.GetDefaultCharacterFormat();
                    }
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
            else if (e.Key == VirtualKey.Escape && _emojiFlyout != null)
            {
                _emojiFlyout.Hide();
                e.Handled = true;
            }
            else if (_emojiFlyout?.Content is ChatTextFlyout presenter && presenter.ItemsSource is IAutocompleteCollection autocomplete)
            {
                e.Handled = ChatTextBox.ProcessFlyoutKey(e.Key, presenter.ControlledList, autocomplete);
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var selection = Document.Selection.GetClone();
            var entity = AutocompleteEntityFinder.Search(selection, out string result, out int index);

            if (entity == AutocompleteEntity.Sticker)
            {
                ShowOrUpdateEmojiFlyout(index, new SearchStickersCollection(viewModel.ClientService, true, result, ChatId));
                return;
            }
            else if (entity == AutocompleteEntity.Emoji)
            {
                ShowOrUpdateEmojiFlyout(index, new ChatTextBox.EmojiCollection(viewModel.ClientService, viewModel.Settings.RecentEmoji, result, ChatId));
                return;
            }

            _emojiQuery = null;
            _emojiFlyout?.Hide();
            CancelEmoji();
        }

        private Flyout _emojiFlyout;
        private string _emojiQuery;

        public override bool HasOpenSuggestions => _emojiFlyout != null;
        private CancellationTokenSource _emojiToken;

        public void CancelEmoji()
        {
            if (_emojiToken != null)
            {
                _emojiToken.Cancel();
                _emojiToken.Dispose();
                _emojiToken = null;
            }
        }

        // Separate from cancelling, which happens on almost every keystroke and has no
        // query of its own to hand a token to
        private CancellationTokenSource BeginEmoji()
        {
            CancelEmoji();
            return _emojiToken = new CancellationTokenSource();
        }

        private async void ShowOrUpdateEmojiFlyout(int index, IAutocompleteCollection collection)
        {
            if (_emojiQuery == collection.Query)
            {
                return;
            }

            var token = BeginEmoji();
            var source = new AutocompleteCollection(collection);

            var result = await source.LoadMoreItemsAsync(0);
            if (result.Count == 0 || token.IsCancellationRequested)
            {
                // Only reset if this is the active query
                if (token == _emojiToken)
                {
                    _emojiQuery = null;
                    _emojiFlyout?.Hide();
                }

                return;
            }

            _emojiQuery = collection.Query;

            if (_emojiFlyout?.Content is ChatTextFlyout presenter)
            {
                presenter.Update(collection);
                return;
            }

            var range = Document.GetRange(index, index);
            range.GetRect(PointOptions.None, out Rect rect, out _);

            _emojiFlyout = ChatTextFlyout.CreateFlyout(new ChatTextFlyout(this, source));

            _emojiFlyout.Opened += EmojiFlyout_Opened;
            _emojiFlyout.Closed += EmojiFlyout_Closed;

            _emojiFlyout.ShowAt(this, new FlyoutShowOptions
            {
                Position = new Point(rect.X + Padding.Left - 8, rect.Y + 6),
                Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        // Added and taken away rather than cleared: the page's own list may be there as well.
        private ListViewBase _flyoutList;

        void EmojiFlyout_Opened(object sender, object args)
        {
            if (ChatTextFlyout.Elevate(sender) is ChatTextFlyout presenter)
            {
                _flyoutList = presenter.ControlledList;
                AutomationProperties.GetControlledPeers(this).Add(_flyoutList);
            }
        }

        private void EmojiFlyout_Closed(object sender, object e)
        {
            _emojiFlyout.Opened -= EmojiFlyout_Opened;
            _emojiFlyout.Closed -= EmojiFlyout_Closed;

            _emojiFlyout = null;

            if (_flyoutList != null)
            {
                AutomationProperties.GetControlledPeers(this).Remove(_flyoutList);
                _flyoutList = null;
            }
        }
    }
}
