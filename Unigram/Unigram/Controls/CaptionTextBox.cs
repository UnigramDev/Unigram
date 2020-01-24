using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Controls.Chats;
using Unigram.Controls.Views;
using Unigram.Native;
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

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            WindowContext.GetForCurrentView().AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            WindowContext.GetForCurrentView().AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if ((args.VirtualKey == VirtualKey.Enter || args.VirtualKey == VirtualKey.Tab) && args.EventType == CoreAcceleratorKeyEventType.KeyDown && FocusState != FocusState.Unfocused)
            {
                // Check if CTRL or Shift is also pressed in addition to Enter key.
                var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);
                var key = Window.Current.CoreWindow.GetKeyState(VirtualKey.Enter);

                if (Autocomplete != null && View.Autocomplete != null)
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send || args.VirtualKey == VirtualKey.Tab)
                    {
                        AcceptsReturn = false;
                        var container = Autocomplete.ContainerFromIndex(Math.Max(0, Autocomplete.SelectedIndex)) as ListViewItem;
                        if (container != null)
                        {
                            var peer = new ListViewItemAutomationPeer(container);
                            var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                            invokeProv.Invoke();
                        }
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }

                    return;
                }

                // If there is text and CTRL/Shift is not pressed, send message. Else allow new row.
                if (ViewModel.Settings.IsSendByEnterEnabled)
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && !ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send)
                    {
                        View?.Accept();
                        AcceptsReturn = false;
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }
                }
                else
                {
                    var send = key.HasFlag(CoreVirtualKeyStates.Down) && ctrl.HasFlag(CoreVirtualKeyStates.Down) && !shift.HasFlag(CoreVirtualKeyStates.Down);
                    if (send)
                    {
                        View?.Accept();
                        AcceptsReturn = false;
                    }
                    else
                    {
                        AcceptsReturn = true;
                    }
                }
            }
        }

        public ListView Autocomplete { get; set; }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Up || e.Key == VirtualKey.Down)
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

            if (!e.Handled)
            {
                base.OnKeyDown(e);
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            Document.GetText(TextGetOptions.NoHidden, out string text);

            if (ChatTextBox.SearchByUsername(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string username, out int index))
            {
                var chat = ViewModel.Chat;
                if (chat == null)
                {
                    return;
                }

                if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup supergroup && !supergroup.IsChannel)
                {
                    View.Autocomplete = new ChatTextBox.UsernameCollection(ViewModel.ProtoService, ViewModel.Chat.Id, username, false, true);
                }
                else
                {
                    View.Autocomplete = null;
                }
            }
            else if (ChatTextBox.SearchByEmoji(text.Substring(0, Math.Min(Document.Selection.EndPosition, text.Length)), out string replacement) && replacement.Length > 0)
            {
                View.Autocomplete = new ChatTextBox.EmojiCollection(ViewModel.ProtoService, replacement.Length < 2 ? replacement : replacement.ToLower(), CoreTextServicesManager.GetForCurrentView().InputLanguage.LanguageTag);
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
