//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Chats
{
    /// <summary>A page that shows a caption's mention list.</summary>
    public interface IViewWithAutocomplete
    {
        IAutocompleteCollection Autocomplete { get; set; }
    }

    /// <summary>
    /// What makes a text box the caption of media being sent: the members and bots offered as a
    /// mention is typed, in the page's own list, which the arrows move through. It works on any
    /// <see cref="IFormattedTextInput"/>, as <see cref="ComposerController"/> does; the emoji
    /// offered as they are typed are each box's own.
    /// </summary>
    public sealed partial class CaptionController
    {
        private readonly IFormattedTextInput _input;
        private readonly UIElement _element;
        private readonly IViewWithAutocomplete _view;
        private readonly ComposeViewModel _viewModel;

        public CaptionController(IFormattedTextInput input, UIElement element, IViewWithAutocomplete view, ComposeViewModel viewModel)
        {
            _input = input;
            _element = element;
            _view = view;
            _viewModel = viewModel;

            _input.SelectionChanged += OnSelectionChanged;

            // Tunnelling, so the list takes Enter and Tab before the box sends or moves focus.
            _element.PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>The list the mentions are shown in, which the arrows move through.</summary>
        public ListViewBase ListAutocomplete { get; set; }

        public void Detach()
        {
            _input.SelectionChanged -= OnSelectionChanged;
            _element.PreviewKeyDown -= OnPreviewKeyDown;
        }

        private void OnSelectionChanged(object sender, EventArgs e)
        {
            var chat = _viewModel?.Chat;
            if (chat == null || !_input.IsSelectionEmpty || !_input.TryGetQuery(out AutocompleteEntity entity, out string query, out _) || entity != AutocompleteEntity.Username)
            {
                _view.Autocomplete = null;
                return;
            }

            var guestBots = chat.Type is not ChatTypeSecret;
            var members = chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup { IsChannel: false };

            if (!guestBots && !members)
            {
                _view.Autocomplete = null;
            }
            else if (_view.Autocomplete is not ChatTextBox.UsernameCollection prev || !prev.Query.Equals(query))
            {
                _view.Autocomplete = new ChatTextBox.UsernameCollection(_viewModel.ClientService, chat.Id, _viewModel.TopicId, query, false, guestBots, members, false);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var autocomplete = _view.Autocomplete;
            var list = ListAutocomplete;

            // 229 is a key the IME is consuming; the box's own suggestions take their keys first.
            if (autocomplete == null || list == null || list.Items.Count == 0 || e.Key == (VirtualKey)229 || _input.HasOpenSuggestions)
            {
                return;
            }

            var modifiers = WindowContext.KeyModifiers();

            switch (e.Key)
            {
                case VirtualKey.Up or VirtualKey.Down when modifiers == VirtualKeyModifiers.None && autocomplete.Orientation == Orientation.Vertical:
                    {
                        list.SelectionMode = ListViewSelectionMode.Single;

                        var next = list.SelectedIndex + (e.Key == VirtualKey.Up ? -1 : 1);
                        if (next >= 0 && next < autocomplete.Count)
                        {
                            list.SelectedIndex = next;
                            list.ScrollIntoView(list.SelectedItem);
                        }

                        e.Handled = true;
                        break;
                    }

                case VirtualKey.Left or VirtualKey.Right when modifiers == VirtualKeyModifiers.None && autocomplete.Orientation == Orientation.Horizontal:
                    e.Handled = ComposerController.MoveHorizontally(list, autocomplete, e.Key == VirtualKey.Right);
                    break;

                case VirtualKey.Tab or VirtualKey.Enter when modifiers != VirtualKeyModifiers.Shift && (autocomplete.InsertOnKeyDown || list.SelectedItem != null):
                    // Invoked as a click would be, so the page's own handler picks it.
                    if (list.ContainerFromIndex(Math.Max(0, list.SelectedIndex)) is GridViewItem container
                        && new GridViewItemAutomationPeer(container).GetPattern(PatternInterface.Invoke) is IInvokeProvider provider)
                    {
                        provider.Invoke();
                    }

                    e.Handled = true;
                    break;
            }
        }

        /// <summary>A member picked from the list, in place of what was typed.</summary>
        public void Accept(object item)
        {
            if (item is User user && _input.TryGetQuery(out AutocompleteEntity entity, out string query, out int index) && entity == AutocompleteEntity.Username)
            {
                var username = user.ActiveUsername(query);

                if (string.IsNullOrEmpty(username))
                {
                    var name = string.IsNullOrEmpty(user.FirstName) ? user.LastName : user.FirstName;
                    if (FormattedTextBox.IsUnsafe(name))
                    {
                        name = Strings.Username;
                    }

                    _input.ReplaceQuery(index, new FormattedText(name + " ", new[] { new TextEntity(0, name.Length, new TextEntityTypeMentionName(user.Id)) }));
                }
                else
                {
                    _input.ReplaceQuery(index, new FormattedText($"@{username} ", Array.Empty<TextEntity>()));
                }
            }

            _view.Autocomplete = null;
        }
    }
}
