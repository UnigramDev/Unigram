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
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Controls.RichEditor
{
    /// <summary>What suggestions need besides the client: whose recent emoji, and which chat they are for.</summary>
    public sealed record RichSuggestionContext(ISettingsService Settings, long ChatId);

    /// <summary>
    /// Suggestions for what is being typed, found by <see cref="AutocompleteEntityFinder"/>:
    /// emoji for a ":query", and custom emoji for an emoji just typed, where the schema allows
    /// custom emoji. A picked one replaces what was typed through
    /// <see cref="RichEditorEngine.Substitute"/>, so Backspace gives the typing back.
    /// </summary>
    public sealed partial class RichEditorView : IAutocompleteTarget
    {
        private bool _suggestionPending;

        private Flyout _suggestions;
        private string _suggestionQuery;
        private CancellationTokenSource _suggestionToken;

        // Where the query was found, so a pick made after the text moved on is dropped.
        private RichTextLeaf _suggestionLeaf;
        private int _suggestionIndex;
        private AutocompleteEntity _suggestionEntity;
        private string _suggestionResult;

        /// <summary>Set to offer suggestions; null, the default, offers none.</summary>
        public RichSuggestionContext Suggestions { get; set; }

        public bool IsSuggesting => _suggestions != null;

        IClientService IAutocompleteTarget.ClientService => _clientService;

        private bool AllowsCustomEmoji => _engine?.Schema?.AllowsCustomEmoji ?? true;

        /// <summary>After a change: looked for once the change is drawn, never inside the input service's callback.</summary>
        private void UpdateSuggestions(RichEditorChanges changes)
        {
            if (_suggestionPending || (changes & (RichEditorChanges.Text | RichEditorChanges.Selection)) == 0)
            {
                return;
            }

            if (Suggestions == null)
            {
                HideSuggestions();
                return;
            }

            _suggestionPending = DispatcherQueue.GetForCurrentThread()?.TryEnqueue(FindSuggestions) ?? false;
        }

        private void FindSuggestions()
        {
            _suggestionPending = false;

            var context = Suggestions;
            if (context == null || _engine == null || _clientService == null)
            {
                HideSuggestions();
                return;
            }

            var focus = _engine.Focus;
            var leaf = focus.Leaf;

            if (leaf == null || leaf.IsPlain || !_hasFocus
                || !_engine.IsSelectionEmpty || _engine.SelectedNode != null || IsInCode(leaf, focus.Offset)
                || !AutocompleteEntityFinder.TrySearch(leaf.Text, focus.Offset, RichTextLeaf.ObjectChar, out AutocompleteEntity entity, out string result, out int index))
            {
                HideSuggestions();
                return;
            }

            IAutocompleteCollection collection = entity switch
            {
                AutocompleteEntity.Emoji => new ChatTextBox.EmojiCollection(_clientService, context.Settings.RecentEmoji, result, context.ChatId, AllowsCustomEmoji),
                AutocompleteEntity.Sticker when AllowsCustomEmoji => new SearchStickersCollection(_clientService, true, result, context.ChatId),
                _ => null
            };

            if (collection == null)
            {
                HideSuggestions();
                return;
            }

            _suggestionLeaf = leaf;
            _suggestionIndex = index;
            _suggestionEntity = entity;
            _suggestionResult = result;

            ShowSuggestions(collection, leaf, index);
        }

        private static bool IsInCode(RichTextLeaf leaf, int offset)
        {
            if (offset == 0)
            {
                return false;
            }

            var run = leaf.FindRun(offset - 1, out _);
            return run >= 0 && (leaf.Runs[run].Marks & RichMarks.Monospace) != 0;
        }

        private async void ShowSuggestions(IAutocompleteCollection collection, RichTextLeaf leaf, int index)
        {
            if (_suggestionQuery == collection.Query)
            {
                return;
            }

            CancelSuggestions();

            var token = _suggestionToken = new CancellationTokenSource();
            var source = new AutocompleteCollection(collection);

            var result = await source.LoadMoreItemsAsync(0);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (result.Count == 0)
            {
                HideSuggestions();
                return;
            }

            _suggestionQuery = collection.Query;

            if (_suggestions?.Content is ChatTextFlyout presenter)
            {
                presenter.Update(collection);
                return;
            }

            if (!_leafViews.TryGetValue(leaf, out RichLeafView view))
            {
                return;
            }

            var rect = view.GetCaretRect(index);
            if (rect.IsEmpty)
            {
                return;
            }

            var origin = view.TransformToVisual(this).TransformPoint(new Point(rect.X, rect.Y));

            _suggestions = ChatTextFlyout.CreateFlyout(new ChatTextFlyout(this, source));
            _suggestions.Opened += OnSuggestionsOpened;
            _suggestions.Closed += OnSuggestionsClosed;

            // Over the query's line with a gap, its first item over the query. The old boxes'
            // "+ 6" is from RichEdit's text area, which starts below the box's top: here the
            // point is the line's own top, so the gap is taken off it instead.
            _suggestions.ShowAt(this, new FlyoutShowOptions
            {
                Position = new Point(origin.X - 8, origin.Y - 6),
                Placement = FlyoutPlacementMode.TopEdgeAlignedLeft,
                ShowMode = FlyoutShowMode.Transient
            });
        }

        // Added and taken away rather than cleared: a page's own list, the mentions under a
        // caption, may be there as well.
        private ListViewBase _suggestionList;

        private void OnSuggestionsOpened(object sender, object e)
        {
            if (ChatTextFlyout.Elevate(sender) is ChatTextFlyout presenter)
            {
                RemoveSuggestionPeer();

                _suggestionList = presenter.ControlledList;
                AutomationProperties.GetControlledPeers(this).Add(_suggestionList);
            }
        }

        private void RemoveSuggestionPeer()
        {
            if (_suggestionList != null)
            {
                AutomationProperties.GetControlledPeers(this).Remove(_suggestionList);
                _suggestionList = null;
            }
        }

        private void OnSuggestionsClosed(object sender, object e)
        {
            if (sender is Flyout flyout)
            {
                flyout.Opened -= OnSuggestionsOpened;
                flyout.Closed -= OnSuggestionsClosed;
            }

            if (ReferenceEquals(sender, _suggestions))
            {
                _suggestions = null;
                _suggestionQuery = null;
            }

            // Its own list only: the flyout open now may be another one.
            if (sender is Flyout { Content: ChatTextFlyout presenter } && ReferenceEquals(presenter.ControlledList, _suggestionList))
            {
                RemoveSuggestionPeer();
            }
        }

        private void CancelSuggestions()
        {
            if (_suggestionToken != null)
            {
                _suggestionToken.Cancel();
                _suggestionToken.Dispose();
                _suggestionToken = null;
            }
        }

        public void HideSuggestions()
        {
            CancelSuggestions();

            _suggestionQuery = null;
            _suggestionLeaf = null;

            // Let go of now, not when it has closed, which is later: what is found meanwhile -
            // the custom emoji for the emoji just picked - would be shown in the flyout that is
            // going away, and never seen.
            if (_suggestions != null)
            {
                var flyout = _suggestions;
                _suggestions = null;

                RemoveSuggestionPeer();
                flyout.Hide();
            }
        }

        /// <summary>
        /// The keys the open suggestions take: the arrows along the list, Tab or Enter to pick,
        /// Escape to close. A box with keys of its own asks this first, so Enter picks instead
        /// of sending.
        /// </summary>
        public bool ProcessSuggestionKey(VirtualKey key, bool ctrl, bool shift, bool alt)
        {
            if (_suggestions?.Content is not ChatTextFlyout presenter || presenter.ItemsSource is not IAutocompleteCollection collection)
            {
                return false;
            }

            var list = presenter.ControlledList;
            var count = list.Items.Count;
            var modifiers = ctrl || shift || alt;

            if (key == VirtualKey.Escape)
            {
                HideSuggestions();
                return true;
            }

            if (count == 0 || modifiers)
            {
                return false;
            }

            var previous = collection.Orientation == Orientation.Vertical ? VirtualKey.Up : VirtualKey.Left;
            var next = collection.Orientation == Orientation.Vertical ? VirtualKey.Down : VirtualKey.Right;

            if (key == previous || key == next)
            {
                // Off the first item, back to the text; past the last, round to the first.
                var index = list.SelectedIndex + (key == next ? 1 : -1);
                if (index < -1)
                {
                    return false;
                }

                list.SelectionMode = ListViewSelectionMode.Single;
                list.SelectedIndex = index >= count ? 0 : index;

                if (list.SelectedItem != null)
                {
                    list.ScrollIntoView(list.SelectedItem);
                }

                return true;
            }

            if (key is VirtualKey.Tab or VirtualKey.Enter && (collection.InsertOnKeyDown || list.SelectedItem != null))
            {
                AcceptSuggestion(list.Items[Math.Max(0, list.SelectedIndex)]);
                return true;
            }

            return false;
        }

        void IAutocompleteTarget.Accept(object item) => AcceptSuggestion(item);

        private void AcceptSuggestion(object item)
        {
            var leaf = _suggestionLeaf;
            var index = _suggestionIndex;
            var entity = _suggestionEntity;
            var result = _suggestionResult;

            HideSuggestions();

            var focus = _engine.Focus;
            if (leaf == null || !ReferenceEquals(focus.Leaf, leaf) || index >= focus.Offset || Suggestions is not RichSuggestionContext context)
            {
                return;
            }

            if (item is EmojiData emoji)
            {
                context.Settings.RecentEmoji.AddRecentEmoji(emoji);
                _engine.Substitute(index, emoji.Value);
            }
            else if (item is Sticker { FullType: StickerFullTypeCustomEmoji customEmoji } sticker)
            {
                context.Settings.RecentEmoji.AddRecentEmoji(sticker.Emoji, customEmoji.CustomEmojiId);

                var inline = RichInlineObject.FromCustomEmoji(customEmoji.CustomEmojiId, sticker.Emoji);
                _engine.Substitute(index, null, inline);

                if (entity == AutocompleteEntity.Sticker)
                {
                    SubstitutePreceding(leaf, index, result, inline);
                }
            }

            Focus(FocusState.Programmatic);
        }

        /// <summary>
        /// The same emoji typed several times over goes in as the custom emoji picked for the
        /// last of them, as the old boxes did. Each is its own replacement, so Backspace gives
        /// them back one at a time.
        /// </summary>
        private void SubstitutePreceding(RichTextLeaf leaf, int end, string emoji, RichInlineObject inline)
        {
            var tail = leaf.Length - _engine.Focus.Offset;

            while (AutocompleteEntityFinder.TrySearch(leaf.Text, end, RichTextLeaf.ObjectChar, out AutocompleteEntity entity, out string result, out int index)
                && entity == AutocompleteEntity.Sticker && result == emoji)
            {
                _engine.SetCaret(new RichPosition(leaf, end));
                _engine.Substitute(index, null, inline);
                end = index;
            }

            _engine.SetCaret(new RichPosition(leaf, leaf.Length - tail));
        }
    }
}
