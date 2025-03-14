//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using System;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.System;

namespace Telegram.Controls
{
    public partial class CaptionTextBox : FormattedTextBox
    {
        public ComposeViewModel ViewModel { get; set; }

        public IViewWithAutocomplete View { get; set; }

        public CaptionTextBox()
        {
            SelectionChanged += OnSelectionChanged;
        }

        public ListViewBase Autocomplete { get; set; }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Up or VirtualKey.Down)
            {
                var modifiers = WindowContext.KeyModifiers();
                if (modifiers == VirtualKeyModifiers.None)
                {
                    if (Autocomplete != null && View.Autocomplete != null)
                    {
                        Autocomplete.SelectionMode = ListViewSelectionMode.Single;

                        var index = e.Key == VirtualKey.Up ? -1 : 1;
                        var next = Autocomplete.SelectedIndex + index;
                        if (next >= 0 && next < View.Autocomplete.Count)
                        {
                            Autocomplete.SelectedIndex = next;
                            Autocomplete.ScrollIntoView(Autocomplete.SelectedItem);
                        }

                        e.Handled = true;
                    }
                }
            }
            else if ((e.Key == VirtualKey.Tab || e.Key == VirtualKey.Enter) && Autocomplete != null && Autocomplete.Items.Count > 0 && View.Autocomplete != null && View.Autocomplete is not SearchStickersCollection)
            {
                var container = Autocomplete.ContainerFromIndex(Math.Max(0, Autocomplete.SelectedIndex)) as SelectorItem;
                if (container != null)
                {
                    var peer = FrameworkElementAutomationPeer.FromElement(container);
                    var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    provider.Invoke();
                }

                Logger.Debug("Tab pressed and handled");
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Tab)
            {
                var ctrl = WindowContext.IsKeyDown(VirtualKey.Control);
                if (ctrl)
                {
                    return;
                }
            }

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        protected override void OnAccept()
        {
            if (View != null)
            {
                View?.Accept();
            }
            else
            {
                base.OnAccept();
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            Document.GetText(TextGetOptions.NoHidden, out string text);

            var query = text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length));

            if (AutocompleteEntityFinder.TrySearch(query, out AutocompleteEntity entity, out string result, out int index))
            {
                if (entity == AutocompleteEntity.Username)
                {
                    var chat = viewModel.Chat;
                    if (chat == null)
                    {
                        View.Autocomplete = null;
                        return;
                    }

                    if (chat.Type is ChatTypeBasicGroup or ChatTypeSupergroup { IsChannel: false })
                    {
                        View.Autocomplete = new ChatTextBox.UsernameCollection(viewModel.ClientService, viewModel.Chat.Id, viewModel.ThreadId, result, false, true, false);
                        return;
                    }
                }
                else if (entity == AutocompleteEntity.Emoji)
                {
                    View.Autocomplete = new ChatTextBox.EmojiCollection(viewModel.ClientService, result, viewModel.Chat.Id);
                    return;
                }
            }

            View.Autocomplete = null;
        }
    }

    public interface IViewWithAutocomplete
    {
        IAutocompleteCollection Autocomplete { get; set; }
        void Accept();
    }
}
