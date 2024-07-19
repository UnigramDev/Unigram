//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.System;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public class CaptionTextBox : FormattedTextBox
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
                var alt = WindowContext.IsKeyDown(VirtualKey.Menu);
                var ctrl = WindowContext.IsKeyDown(VirtualKey.Control);
                var shift = WindowContext.IsKeyDown(VirtualKey.Shift);

                if (!alt && !ctrl && !shift)
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
            View?.Accept();
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

                    var members = true;
                    if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret || chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                    {
                        members = false;
                    }

                    if (members)
                    {
                        View.Autocomplete = new ChatTextBox.UsernameCollection(viewModel.ClientService, viewModel.Chat.Id, viewModel.ThreadId, result, false, members);
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
