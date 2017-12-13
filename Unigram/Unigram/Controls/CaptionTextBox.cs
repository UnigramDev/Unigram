using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Channels;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Native;
using Unigram.ViewModels;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Automation.Provider;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public class CaptionTextBox : TextBox
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        public SendMediaView View { get; set; }

        public CaptionTextBox()
        {
            TextChanged += OnTextChanged;
            SelectionChanged += OnSelectionChanged;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            App.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            App.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
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
                if (ApplicationSettings.Current.IsSendByEnterEnabled)
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

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (View?.SelectedItem != null)
            {
                View.SelectedItem.Caption = Text.ToString();
            }
        }

        private void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            var text = Text.ToString();

            if (BubbleTextBox.SearchByUsername(text.Substring(0, Math.Min(SelectionStart, text.Length)), out string username))
            {
                View.Autocomplete = GetUsernames(username);
            }
            else if (BubbleTextBox.SearchByEmoji(text.Substring(0, Math.Min(SelectionStart, text.Length)), out string replacement) && replacement.Length > 0)
            {
                View.Autocomplete = EmojiSuggestion.GetSuggestions(replacement);
            }
            else
            {
                View.Autocomplete = null;
            }
        }

        private List<TLUser> GetUsernames(string username)
        {
            var query = LocaleHelper.GetQuery(username);
            bool IsMatch(TLUser user)
            {
                if (user.Username == null)
                {
                    return false;
                }

                return user.IsLike(query, StringComparison.OrdinalIgnoreCase);
            }

            var results = new List<TLUser>();

            if (ViewModel.Full is TLChatFull chatFull && chatFull.Participants is TLChatParticipants chatParticipants)
            {
                foreach (var participant in chatParticipants.Participants)
                {
                    if (participant.User != null && IsMatch(participant.User))
                    {
                        results.Add(participant.User);
                    }
                }
            }
            else if (ViewModel.Full is TLChannelFull channelFull && channelFull.Participants is TLChannelsChannelParticipants channelParticipants)
            {
                foreach (var participant in channelParticipants.Participants)
                {
                    if (participant.User != null && IsMatch(participant.User))
                    {
                        results.Add(participant.User);
                    }
                }
            }

            if (results.Count > 0)
            {
                return results;
            }

            return null;
        }
    }
}
