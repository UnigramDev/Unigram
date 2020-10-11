using System;
using System.Collections;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Controls.Chats;
using Unigram.ViewModels;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class CaptionTextBox : FormattedTextBox
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public IViewWithAutocomplete View { get; set; }

        public CaptionTextBox()
        {
            SelectionChanged += OnSelectionChanged;
        }

        public ListView Autocomplete { get; set; }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
            {
                var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

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
            else if ((e.Key == VirtualKey.Tab || e.Key == VirtualKey.Enter) && Autocomplete != null && Autocomplete.Items.Count > 0 && View.Autocomplete != null && !(View.Autocomplete is SearchStickersCollection))
            {
                var container = Autocomplete.ContainerFromIndex(Math.Max(0, Autocomplete.SelectedIndex)) as ListViewItem;
                if (container != null)
                {
                    var peer = new ListViewItemAutomationPeer(container);
                    var provider = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                    provider.Invoke();
                }

                Logs.Logger.Debug(Logs.Target.Chat, "Tab pressed and handled");
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Tab)
            {
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                if (ctrl)
                {
                    return;
                }
            }
            else if (e.Key == VirtualKey.Enter)
            {
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                var send = false;

                if (ViewModel.Settings.IsSendByEnterEnabled)
                {
                    send = !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                }
                else
                {
                    send = ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                }

                AcceptsReturn = !send;
                e.Handled = send;

                if (send)
                {
                    View?.Accept();
                }
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

            Document.GetText(TextGetOptions.NoHidden, out string text);

            if (ChatTextBox.SearchByUsername(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string username, out int index))
            {
                var chat = viewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel)
                {
                    View.Autocomplete = new ChatTextBox.UsernameCollection(viewModel.ProtoService, viewModel.Chat.Id, username, false, true);
                }
                else
                {
                    View.Autocomplete = null;
                }
            }
            else if (ChatTextBox.SearchByEmoji(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string replacement) && replacement.Length > 0)
            {
                View.Autocomplete = new ChatTextBox.EmojiCollection(viewModel.ProtoService, replacement.Length < 2 ? replacement : replacement.ToLower(), CoreTextServicesManager.GetForCurrentView().InputLanguage.LanguageTag);
            }
            else
            {
                View.Autocomplete = null;
            }
        }
    }

    public interface IViewWithAutocomplete
    {
        ICollection Autocomplete { get; set; }
        void Accept();
    }
}
