//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public enum UndoType
    {
        Delete,
        Clear,
        Archive
    }

    public sealed partial class UndoView : UserControl
    {
        private readonly DispatcherTimer _timeout;
        private readonly Queue<UndoOp> _queue;

        private int _remaining = 5;

        public UndoView()
        {
            InitializeComponent();

            _timeout = new DispatcherTimer();
            _timeout.Interval = TimeSpan.FromSeconds(1);
            _timeout.Tick += Timeout_Tick;

            _queue = new Queue<UndoOp>();
        }

        private void Timeout_Tick(object sender, object e)
        {
            _remaining--;

            Remaining.Content = _remaining;
            //Slice.StartAngle = 360 - ((360d / 5d) * _remaining);

            if (_remaining == 0)
            {
                Reset(false);
            }
        }

        public void Show(IList<Chat> chats, UndoType type, Action<IList<Chat>> undo, Action<IList<Chat>> action = null)
        {
            _remaining = 5;
            _queue.Enqueue(new UndoOp(chats, undo, action));

            _timeout.Stop();
            _timeout.Start();

            if (type == UndoType.Archive)
            {
                Text.Text = chats.Count > 1 ? Strings.ChatsArchived : Strings.ChatArchived;

                Remaining.Visibility = Visibility.Collapsed;
                Slice.Visibility = Visibility.Collapsed;
                Player.Source = new Assets.Animations.ChatArchivedAnimation();

                _ = Player.PlayAsync(0, 1, false);
            }
            else
            {
                if (type == UndoType.Clear)
                {
                    Text.Text = Strings.HistoryClearedUndo;
                }
                else if (chats.Count == 1 && chats[0] is Chat chat)
                {
                    if (chat.Type is ChatTypeSupergroup super)
                    {
                        Text.Text = super.IsChannel ? Strings.ChannelDeletedUndo : Strings.GroupDeletedUndo;
                    }
                    else
                    {
                        Text.Text = chat.Type is ChatTypeBasicGroup ? Strings.GroupDeletedUndo : Strings.ChatDeletedUndo;
                    }
                }
                else
                {
                    Text.Text = Strings.ChatDeletedUndo;
                }

                Remaining.Visibility = Visibility.Visible;
                Slice.Visibility = Visibility.Visible;
                Player.Source = null;
            }

            IsEnabled = true;

            Slice.Value = null;
            Remaining.Content = 5;

            StartAnimation();
            Grid.SetRow(LayoutRoot, 0);
        }

        private void StartAnimation()
        {
            Slice.Maximum = 5;
            Slice.Value = DateTime.Now.AddSeconds(5);
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            Reset(true);
        }

        private void Reset(bool undo)
        {
            while (_queue.Count > 0)
            {
                var current = _queue.Dequeue();
                if (undo)
                {
                    current.Undo.Invoke(current.Chats);
                }
                else if (current.Action != null)
                {
                    current.Action.Invoke(current.Chats);
                }
            }

            _remaining = 5;

            _timeout.Stop();

            IsEnabled = true;
            Slice.Value = null;

            Grid.SetRow(LayoutRoot, 1);
        }

        private class UndoOp
        {
            public IList<Chat> Chats { get; private set; }
            public Action<IList<Chat>> Undo { get; private set; }
            public Action<IList<Chat>> Action { get; private set; }

            public UndoOp(IList<Chat> chats, Action<IList<Chat>> undo, Action<IList<Chat>> action)
            {
                Chats = chats;
                Undo = undo;
                Action = action;
            }
        }
    }
}
