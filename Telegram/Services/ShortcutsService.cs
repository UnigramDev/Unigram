//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Windows.Data.Json;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Input;

namespace Telegram.Services
{
    public interface IShortcutsService
    {
        InvokedShortcut Process(AcceleratorKeyEventArgs args, out VirtualKeyModifiers modifiers);
        InvokedShortcut Process(VirtualKey key, out VirtualKeyModifiers modifiers);

        bool TryGetShortcut(KeyRoutedEventArgs args, out Shortcut shortcut);
        bool TryGetShortcut(ProcessKeyboardAcceleratorEventArgs args, out Shortcut shortcut);

        /// <summary>
        /// Completes once shortcuts.json has been applied. The dispatch path does not need it -
        /// it reads the table as it stands - but anything that snapshots the table does.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// True while a chord is being recorded, and nothing in the app may answer a shortcut.
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Takes the keyboard away from the shortcut table so a chord can be captured instead.
        /// </summary>
        /// <remarks>
        /// The scope is a lease, not a flag. Holding it silences every shortcut in the process, so
        /// a capture surface that dies without disposing it would otherwise take the keyboard with
        /// it until restart - and back navigation too, since the listener stands down entirely.
        /// Nothing renews a lapsed lease, so the keyboard comes back on its own.
        /// </remarks>
        IShortcutRecording Record();

        ShortcutConflict GetConflict(Shortcut shortcut, ShortcutCommand command);

        IList<ShortcutList> GetShortcuts();

        Shortcut GetShortcut(ShortcutCommand command);

        // None of these return the list again: the caller holds the rows already, and rebuilding
        // them would reset the ListView - scroll position, focus and all - over a change to one.
        Task UpdateAsync(Shortcut shortcut, ShortcutCommand command);
        Task UnbindAsync(ShortcutCommand command);
        Task ResetAsync(ShortcutCommand command);
        Task ResetAllAsync();

        /// <summary>
        /// The customisations, in the same form shortcuts.json holds them.
        /// </summary>
        string Export();

        /// <summary>
        /// Applies customisations from a file, and reports what it made of them.
        /// </summary>
        /// <remarks>
        /// Everything goes through the same checks the editor applies, so an imported or
        /// hand-written file cannot bind a chord the editor would have refused.
        /// </remarks>
        Task<ShortcutsImportResult> ImportAsync(string text);
    }

    public partial class ShortcutsImportResult
    {
        public static readonly ShortcutsImportResult Invalid = new(false, 0, 0);

        public ShortcutsImportResult(bool valid, int applied, int ignored)
        {
            Valid = valid;
            Applied = applied;
            Ignored = ignored;
        }

        /// <summary>
        /// False when the text was not a shortcuts file at all, as opposed to one with entries
        /// that could not be used.
        /// </summary>
        public bool Valid { get; }

        public int Applied { get; }

        public int Ignored { get; }
    }

    /// <summary>
    /// A held claim on the keyboard, renewed for as long as the capture surface is alive.
    /// </summary>
    public interface IShortcutRecording : IDisposable
    {
        /// <summary>
        /// Pushes the lease out. The owner calls this on a timer well inside
        /// <see cref="ShortcutsService.RecordingLease"/>, so a stalled UI thread costs a renewal
        /// or two rather than ending the recording.
        /// </summary>
        void Renew();
    }

    public partial class InvokedShortcut : Shortcut
    {
        public IList<ShortcutCommand> Commands { get; }

        public InvokedShortcut(VirtualKeyModifiers modifiers, VirtualKey key, IList<ShortcutCommand> commands)
            : base(modifiers, key)
        {
            Commands = commands;
        }
    }

    public partial class ShortcutsService : IShortcutsService
    {
        #region Const

        private readonly ShortcutCommand[] _autoRepeatCommands = new[]
        {
            //ShortcutCommand.MediaPrevious,
            //ShortcutCommand.MediaNext,
            ShortcutCommand.ChatRecentPrevious,
            ShortcutCommand.ChatRecentNext,
            ShortcutCommand.ChatPrevious,
            ShortcutCommand.ChatNext,
            ShortcutCommand.ChatFirst,
            ShortcutCommand.ChatLast,
        };

        //private readonly ShortcutCommand[] _mediaCommands = new[]
        //{
        //    ShortcutCommand.MediaPlay,
        //    ShortcutCommand.MediaPause,
        //    ShortcutCommand.MediaPlayPause,
        //    ShortcutCommand.MediaStop,
        //    ShortcutCommand.MediaPrevious,
        //    ShortcutCommand.MediaNext,
        //};

        //private readonly ShortcutCommand[] _supportCommands = new[]
        //{
        //    ShortcutCommand.SupportReloadTemplates,
        //    ShortcutCommand.SupportToggleMuted,
        //    ShortcutCommand.SupportScrollToCurrent,
        //    ShortcutCommand.SupportHistoryBack,
        //    ShortcutCommand.SupportHistoryForward,
        //};

        private readonly ShortcutCommand[] _foldersCommands = new[]
        {
            ShortcutCommand.ShowAllChats,
            ShortcutCommand.ShowFolder1,
            ShortcutCommand.ShowFolder2,
            ShortcutCommand.ShowFolder3,
            ShortcutCommand.ShowFolder4,
            ShortcutCommand.ShowFolder5,
            ShortcutCommand.ShowFolder6,
            ShortcutCommand.ShowFolderLast,
        };

        private readonly Dictionary<string, ShortcutCommand> _commandByName = new()
        {
            { "close_telegram"      , ShortcutCommand.Close },
            { "lock_telegram"       , ShortcutCommand.Lock },
            { "minimize_telegram"   , ShortcutCommand.Minimize },
            { "quit_telegram"       , ShortcutCommand.Quit },

            //{ "media_play"        , ShortcutCommand.MediaPlay },
            //{ "media_pause"       , ShortcutCommand.MediaPause },
            { "media_playpause"   , ShortcutCommand.MediaPlayPause },
            { "media_stop"        , ShortcutCommand.MediaStop },
            { "media_previous"    , ShortcutCommand.MediaPrevious },
            { "media_next"        , ShortcutCommand.MediaNext },

            { "search"              , ShortcutCommand.Search },

            { "previous_chat"       , ShortcutCommand.ChatPrevious },
            { "next_chat"           , ShortcutCommand.ChatNext },
            { "previous_recent_chat", ShortcutCommand.ChatRecentPrevious },
            { "next_recent_chat"    , ShortcutCommand.ChatRecentNext },
            { "first_chat"          , ShortcutCommand.ChatFirst },
            { "last_chat"           , ShortcutCommand.ChatLast },
            { "self_chat"           , ShortcutCommand.ChatSelf },

            { "previous_folder"     , ShortcutCommand.FolderPrevious },
            { "next_folder"         , ShortcutCommand.FolderNext },
            { "all_chats"           , ShortcutCommand.ShowAllChats },

            { "folder1"             , ShortcutCommand.ShowFolder1 },
            { "folder2"             , ShortcutCommand.ShowFolder2 },
            { "folder3"             , ShortcutCommand.ShowFolder3 },
            { "folder4"             , ShortcutCommand.ShowFolder4 },
            { "folder5"             , ShortcutCommand.ShowFolder5 },
            { "folder6"             , ShortcutCommand.ShowFolder6 },
            { "last_folder"         , ShortcutCommand.ShowFolderLast },

            { "show_archive"        , ShortcutCommand.ShowArchive },

            { "set_status"          , ShortcutCommand.SetStatus },
            { "downloads"           , ShortcutCommand.Downloads },

            { "search_chats"        , ShortcutCommand.SearchChats },

            { "pinned_chat1"        , ShortcutCommand.ChatPinned1 },
            { "pinned_chat2"        , ShortcutCommand.ChatPinned2 },
            { "pinned_chat3"        , ShortcutCommand.ChatPinned3 },
            { "pinned_chat4"        , ShortcutCommand.ChatPinned4 },
            { "pinned_chat5"        , ShortcutCommand.ChatPinned5 },

            { "call_accept"         , ShortcutCommand.CallAccept },
            { "call_reject"         , ShortcutCommand.CallReject },
            { "call_camera"         , ShortcutCommand.CallToggleCamera },
            { "call_microphone"     , ShortcutCommand.CallToggleMicrophone },

            // Shortcuts that have no default values.
            { "message"             , ShortcutCommand.JustSendMessage },
            { "message_silently"    , ShortcutCommand.SendSilentMessage },
            { "message_scheduled"   , ShortcutCommand.ScheduleMessage },
            //
        };

        // Derived rather than written out: the two were maintained by hand and had drifted -
        // the call names round-tripped out but not back in - which silently dropped bindings.
        private readonly Dictionary<ShortcutCommand, string> _commandNames;

        /// <summary>
        /// Chords owned by code that does not go through this service.
        /// </summary>
        /// <remarks>
        /// <c>InputListener</c> hooks <c>AcceleratorKeyActivated</c> and sets
        /// <c>Handled</c>, so the table here wins over every one of these: binding one would not
        /// conflict with its owner, it would silently kill it. Hand-maintained, and it only has to
        /// be right about the chords worth protecting.
        /// </remarks>
        private static readonly Dictionary<Shortcut, ShortcutOwner> _reserved = new()
        {
            // Telegram/Controls/FormattedTextBox.cs, OnKeyDown.
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.B), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.I), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.U), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.K), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, VirtualKey.X), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, VirtualKey.M), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, VirtualKey.P), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, VirtualKey.N), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, VirtualKey.Z), ShortcutOwner.MessageEditor },

            // Telegram/Controls/Chats/ChatTextBox.cs, reply navigation.
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.Up), ShortcutOwner.MessageEditor },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.Down), ShortcutOwner.MessageEditor },

            // RichEditBox and every other text surface in the app.
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.X), ShortcutOwner.TextEditing },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.C), ShortcutOwner.TextEditing },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.V), ShortcutOwner.TextEditing },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.A), ShortcutOwner.TextEditing },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.Z), ShortcutOwner.TextEditing },
            { new Shortcut(VirtualKeyModifiers.Control, VirtualKey.Y), ShortcutOwner.TextEditing },

            // Telegram/Navigation/InputListener.Uwp.cs, consumed before the table is consulted.
            { new Shortcut(VirtualKeyModifiers.Menu, VirtualKey.Left), ShortcutOwner.Navigation },
            { new Shortcut(VirtualKeyModifiers.Menu, VirtualKey.Right), ShortcutOwner.Navigation },
            { new Shortcut(VirtualKeyModifiers.None, VirtualKey.Escape), ShortcutOwner.Navigation },
        };

        /// <summary>
        /// Context pairs whose commands may share one chord.
        /// </summary>
        /// <remarks>
        /// A promise about the dispatch sites rather than anything the types enforce: the call
        /// commands do nothing unless a call is up, and <c>ProcessFolderCommands</c> returns early
        /// when the account has no folders (<c>MainPage.xaml.cs</c>, <c>if (folders.Empty())</c>).
        /// That is what makes ctrl+pgdown and ctrl+1 legitimately carry two commands each.
        /// </remarks>
        private static readonly (ShortcutContext, ShortcutContext)[] _exclusive = new[]
        {
            (ShortcutContext.Global, ShortcutContext.Call),
            (ShortcutContext.PinnedChats, ShortcutContext.Folders),
        };

        /// <summary>
        /// Every command, in the order the settings page lists them.
        /// </summary>
        /// <remarks>
        /// A command missing from here cannot be given a shortcut, which is how MediaStop,
        /// SearchChats, the pinned chats and the call commands ended up uneditable. The names are
        /// resolved per call rather than stored, so opening the page is what realizes them.
        /// </remarks>
        private static readonly (Func<string> Name, ShortcutCommand[] Commands)[] _categories = new (Func<string>, ShortcutCommand[])[]
        {
            (() => Strings.ShortcutsCategoryApp, new[]
            {
                ShortcutCommand.Search,
                ShortcutCommand.SearchChats,
                ShortcutCommand.Downloads,
                ShortcutCommand.SetStatus,
                ShortcutCommand.MediaPlayPause,
                ShortcutCommand.MediaPrevious,
                ShortcutCommand.MediaNext,
                ShortcutCommand.MediaStop,
                ShortcutCommand.Lock,
                ShortcutCommand.Minimize,
                ShortcutCommand.Close,
                ShortcutCommand.Quit,
            }),
            (() => Strings.ShortcutsCategoryChats, new[]
            {
                ShortcutCommand.ChatPrevious,
                ShortcutCommand.ChatNext,
                ShortcutCommand.ChatRecentPrevious,
                ShortcutCommand.ChatRecentNext,
                ShortcutCommand.ChatFirst,
                ShortcutCommand.ChatLast,
                ShortcutCommand.ChatSelf,
                ShortcutCommand.ChatPinned1,
                ShortcutCommand.ChatPinned2,
                ShortcutCommand.ChatPinned3,
                ShortcutCommand.ChatPinned4,
                ShortcutCommand.ChatPinned5,
                ShortcutCommand.JustSendMessage,
                ShortcutCommand.SendSilentMessage,
                ShortcutCommand.ScheduleMessage,
            }),
            (() => Strings.ShortcutsCategoryFolders, new[]
            {
                ShortcutCommand.FolderPrevious,
                ShortcutCommand.FolderNext,
                ShortcutCommand.ShowAllChats,
                ShortcutCommand.ShowFolder1,
                ShortcutCommand.ShowFolder2,
                ShortcutCommand.ShowFolder3,
                ShortcutCommand.ShowFolder4,
                ShortcutCommand.ShowFolder5,
                ShortcutCommand.ShowFolder6,
                ShortcutCommand.ShowFolderLast,
                ShortcutCommand.ShowArchive,
            }),
            (() => Strings.ShortcutsCategoryCalls, new[]
            {
                ShortcutCommand.CallAccept,
                ShortcutCommand.CallReject,
                ShortcutCommand.CallToggleMicrophone,
                ShortcutCommand.CallToggleCamera,
            }),
        };

        #endregion

        private readonly Dictionary<Shortcut, List<ShortcutCommand>> _commands = new();

        // What InitializeDefault produced, so an edit can be written out as a difference and a
        // single row can be restored without rebuilding the whole table.
        private readonly Dictionary<Shortcut, List<ShortcutCommand>> _defaults = new();

        private readonly Task _initialize;

        public ShortcutsService()
        {
            _commandNames = new Dictionary<ShortcutCommand, string>(_commandByName.Count);

            foreach (var pair in _commandByName)
            {
                _commandNames[pair.Value] = pair.Key;
            }

            InitializeDefault();

            foreach (var pair in _commands)
            {
                _defaults[pair.Key] = new List<ShortcutCommand>(pair.Value);
            }

            _initialize = InitializeCustomAsync();
        }

        public Task InitializeAsync()
        {
            return _initialize;
        }

        #region Recording

        /// <summary>
        /// How long a lease survives without being renewed.
        /// </summary>
        /// <remarks>
        /// Long enough that a UI thread busy for a moment does not end a recording, short enough
        /// that a leaked one is an annoyance rather than a session-long dead keyboard.
        /// </remarks>
        public const int RecordingLease = 10_000;

        private object _recordingOwner;
        private long _recordingUntil;

        // Expiry is read rather than scheduled: nothing has to tick for a lapsed lease to stop
        // counting, so the service owns no timer and a leak cleans itself up.
        public bool IsRecording
        {
            get
            {
                // Asked on every key press, and zero almost always, so the clock is only read
                // once a recording has actually claimed the keyboard.
                var until = Volatile.Read(ref _recordingUntil);
                return until != 0 && (long)Logger.TickCount < until;
            }
        }

        public IShortcutRecording Record()
        {
            var recording = new ShortcutRecording(this);
            recording.Renew();

            return recording;
        }

        private void Renew(object owner)
        {
            _recordingOwner = owner;
            Volatile.Write(ref _recordingUntil, (long)Logger.TickCount + RecordingLease);
        }

        private void Release(object owner)
        {
            // Only the holder may end it: two capture surfaces at once are pathological, but one
            // of them closing must not hand the keyboard back while the other is still listening.
            if (_recordingOwner == owner)
            {
                _recordingOwner = null;
                Volatile.Write(ref _recordingUntil, 0);
            }
        }

        private sealed partial class ShortcutRecording : IShortcutRecording
        {
            private readonly ShortcutsService _service;

            public ShortcutRecording(ShortcutsService service)
            {
                _service = service;
            }

            public void Renew()
            {
                _service.Renew(this);
            }

            public void Dispose()
            {
                _service.Release(this);
            }
        }

        #endregion

        public InvokedShortcut Process(AcceleratorKeyEventArgs args, out VirtualKeyModifiers modifiers)
        {
            modifiers = WindowContext.KeyModifiers();

            if (args.VirtualKey is >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9)
            {
                return Process(modifiers, VirtualKey.Number0 + (args.VirtualKey - VirtualKey.NumberPad0));
            }

            return Process(modifiers, args.VirtualKey);
        }

        // An overload rather than a rewrite of the one above: that one is on the UWP path and is
        // left alone. Win32 has no AcceleratorKeyEventArgs to hand over, only the key.
        public InvokedShortcut Process(VirtualKey key, out VirtualKeyModifiers modifiers)
        {
            modifiers = WindowContext.KeyModifiers();

            if (key is >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9)
            {
                return Process(modifiers, VirtualKey.Number0 + (key - VirtualKey.NumberPad0));
            }

            return Process(modifiers, key);
        }

        private InvokedShortcut Process(VirtualKeyModifiers modifiers, VirtualKey key)
        {
            var shortcut = new Shortcut(modifiers, key);
            if (_commands.TryGetValue(shortcut, out var value))
            {
                return new InvokedShortcut(modifiers, key, value);
            }

            return null;
        }


        //int nonVirtualKey = MapVirtualKey((uint)args.VirtualKey, 2);
        //char mappedChar = Convert.ToChar(nonVirtualKey);

        public bool TryGetShortcut(KeyRoutedEventArgs args, out Shortcut shortcut)
        {
            return TryGetShortcut(WindowContext.KeyModifiers(), args.Key, out shortcut);
        }

        // ProcessKeyboardAccelerators is what a popup gets, and it carries the modifiers itself.
        public bool TryGetShortcut(ProcessKeyboardAcceleratorEventArgs args, out Shortcut shortcut)
        {
            return TryGetShortcut(args.Modifiers, args.Key, out shortcut);
        }

        private bool TryGetShortcut(VirtualKeyModifiers modifiers, VirtualKey key, out Shortcut shortcut)
        {
            // Process folds the numeric keypad onto the number row; a chord recorded as NumberPad1
            // would never be matched by anything.
            if (key is >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9)
            {
                key = VirtualKey.Number0 + (key - VirtualKey.NumberPad0);
            }

            // Tab walks the dialog and Escape dismisses it, so neither can be captured without
            // taking away the only way out of the popup.
            if (key is VirtualKey.Tab or VirtualKey.Escape)
            {
                shortcut = null;
                return false;
            }

            if (key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl)
            {
                shortcut = null;
                return false;
            }
            else if (key is VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu)
            {
                shortcut = null;
                return false;
            }
            else if (key is VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift)
            {
                shortcut = null;
                return false;
            }

            if (modifiers == VirtualKeyModifiers.None && (key < VirtualKey.F1 || key > VirtualKey.F24))
            {
                shortcut = null;
                return false;
            }

            shortcut = new Shortcut(modifiers, key);
            return Enum.IsDefined(typeof(VirtualKey), key);
        }

        public IList<ShortcutList> GetShortcuts()
        {
            var result = new List<ShortcutList>();

            foreach (var category in _categories)
            {
                var items = new ShortcutList(category.Name());

                foreach (var command in category.Commands)
                {
                    items.Add(new ShortcutInfo(GetShortcut(command), command));
                }

                result.Add(items);
            }

            return result;
        }

        /// <summary>
        /// The chord a command answers to, or null when it has none.
        /// </summary>
        /// <remarks>
        /// A command can hold more than one - Close is on both ctrl+w and ctrl+f4 - but an edit
        /// replaces them all with the captured one, so a single chord is what the row can honestly
        /// show. Special chords are skipped: they are the system's, not the user's.
        /// </remarks>
        public Shortcut GetShortcut(ShortcutCommand command)
        {
            foreach (var pair in _commands)
            {
                if (IsSpecialShortcut(pair.Key, command))
                {
                    continue;
                }

                if (pair.Value.Contains(command))
                {
                    return pair.Key;
                }
            }

            return null;
        }

        /// <summary>
        /// Every chord a command answers to, in a table.
        /// </summary>
        /// <remarks>
        /// The writer compares sets rather than a single chord: ChatNext holds both ctrl+pgdown
        /// and alt+down by default, and a dictionary hands them back in whatever order removals
        /// have left it in, so comparing only the first would report a difference that is not one
        /// and drop the other chord on the next launch.
        /// </remarks>
        private static List<Shortcut> GetBindings(ShortcutCommand command, Dictionary<Shortcut, List<ShortcutCommand>> source)
        {
            var result = new List<Shortcut>();

            foreach (var pair in source)
            {
                if (pair.Value.Contains(command))
                {
                    result.Add(pair.Key);
                }
            }

            return result;
        }

        private static bool SameBindings(List<Shortcut> x, List<Shortcut> y)
        {
            if (x.Count != y.Count)
            {
                return false;
            }

            foreach (var shortcut in x)
            {
                if (!y.Contains(shortcut))
                {
                    return false;
                }
            }

            return true;
        }

        public ShortcutConflict GetConflict(Shortcut shortcut, ShortcutCommand command)
        {
            if (shortcut == null)
            {
                return ShortcutConflict.None;
            }

            if (_reserved.TryGetValue(shortcut, out var owner))
            {
                return new ShortcutConflict(owner);
            }

            if (_commands.TryGetValue(shortcut, out var commands))
            {
                foreach (var other in commands)
                {
                    if (other != command && !IsExclusive(ContextOf(other), ContextOf(command)))
                    {
                        return new ShortcutConflict(other);
                    }
                }
            }

            return ShortcutConflict.None;
        }

        public async Task UpdateAsync(Shortcut shortcut, ShortcutCommand command)
        {
            if (shortcut == null || GetConflict(shortcut, command).Kind != ShortcutConflictKind.None)
            {
                return;
            }

            Remove(command);
            Add(shortcut, command);

            await SaveCustomAsync();
        }

        public async Task UnbindAsync(ShortcutCommand command)
        {
            Remove(command);

            await SaveCustomAsync();
        }

        public async Task ResetAsync(ShortcutCommand command)
        {
            Remove(command);

            foreach (var pair in _defaults)
            {
                if (pair.Value.Contains(command))
                {
                    Add(pair.Key, command);
                }
            }

            await SaveCustomAsync();
        }

        public async Task ResetAllAsync()
        {
            _commands.Clear();

            foreach (var pair in _defaults)
            {
                _commands[pair.Key] = new List<ShortcutCommand>(pair.Value);
            }

            try
            {
                var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync("shortcuts.json") as StorageFile;
                if (file != null)
                {
                    await file.DeleteAsync();
                }
            }
            catch
            {
                // The table is already back to its defaults; a file left behind would only come
                // back on the next launch, and the next edit rewrites it anyway.
            }
        }

        private static ShortcutContext ContextOf(ShortcutCommand command)
        {
            return command switch
            {
                ShortcutCommand.CallAccept or
                ShortcutCommand.CallReject or
                ShortcutCommand.CallToggleCamera or
                ShortcutCommand.CallToggleMicrophone => ShortcutContext.Call,

                ShortcutCommand.ChatPinned1 or
                ShortcutCommand.ChatPinned2 or
                ShortcutCommand.ChatPinned3 or
                ShortcutCommand.ChatPinned4 or
                ShortcutCommand.ChatPinned5 => ShortcutContext.PinnedChats,

                ShortcutCommand.ShowAllChats or
                ShortcutCommand.ShowFolder1 or
                ShortcutCommand.ShowFolder2 or
                ShortcutCommand.ShowFolder3 or
                ShortcutCommand.ShowFolder4 or
                ShortcutCommand.ShowFolder5 or
                ShortcutCommand.ShowFolder6 or
                ShortcutCommand.ShowFolderLast => ShortcutContext.Folders,

                ShortcutCommand.JustSendMessage or
                ShortcutCommand.SendSilentMessage or
                ShortcutCommand.ScheduleMessage => ShortcutContext.Chat,

                _ => ShortcutContext.Global
            };
        }

        private static bool IsExclusive(ShortcutContext x, ShortcutContext y)
        {
            foreach (var (first, second) in _exclusive)
            {
                if ((first == x && second == y) || (first == y && second == x))
                {
                    return true;
                }
            }

            return false;
        }

        private void Add(Shortcut shortcut, ShortcutCommand command)
        {
            if (!_commands.TryGetValue(shortcut, out var commands))
            {
                commands = _commands[shortcut] = new List<ShortcutCommand>();
            }

            if (!commands.Contains(command))
            {
                commands.Add(command);
            }
        }

        /// <summary>
        /// Drops every binding of one command, leaving anything else on the same chord alone.
        /// </summary>
        /// <remarks>
        /// ctrl+2 carries both ChatPinned2 and ShowFolder1, so clearing the whole entry would
        /// unbind a command the user never touched. Special chords are left as well - ctrl+f4 is
        /// the system's way to close a window, not a binding anyone chose.
        /// </remarks>
        private void Remove(ShortcutCommand command)
        {
            List<Shortcut> empty = null;

            foreach (var pair in _commands)
            {
                if (IsSpecialShortcut(pair.Key, command))
                {
                    continue;
                }

                if (pair.Value.Remove(command) && pair.Value.Count == 0)
                {
                    empty ??= new List<Shortcut>();
                    empty.Add(pair.Key);
                }
            }

            if (empty != null)
            {
                foreach (var shortcut in empty)
                {
                    _commands.Remove(shortcut);
                }
            }
        }

        private bool IsSpecialShortcut(Shortcut shortcut, ShortcutCommand command)
        {
            if (shortcut.Modifiers == VirtualKeyModifiers.Control && shortcut.Key == VirtualKey.F4)
            {
                return command == ShortcutCommand.Close;
            }
            else if (shortcut.Modifiers == VirtualKeyModifiers.None && shortcut.Key == VirtualKey.Search)
            {
                return command == ShortcutCommand.Search;
            }

            return false;
        }

        private void InitializeDefault()
        {
            Set("ctrl+w", ShortcutCommand.Close);
            Set("ctrl+f4", ShortcutCommand.Close);
            Set("ctrl+l", ShortcutCommand.Lock);
            Set("ctrl+m", ShortcutCommand.Minimize);
            Set("ctrl+q", ShortcutCommand.Quit);

            // Nothing in the shell can press SystemMediaTransportControlsButton.Rewind and friends
            // without a multimedia key or an AVRCP remote, and a UWP app never receives VK_MEDIA_*
            // as a key event, so these chords are the only route for a keyboard without media keys.
            // Ctrl+Shift+W/A/D are one hand and adjacent, which matters when you cannot see them.
            Set("ctrl+shift+space", ShortcutCommand.MediaPlayPause);
            Set("ctrl+shift+a", ShortcutCommand.MediaPrevious);
            Set("ctrl+shift+d", ShortcutCommand.MediaNext);
            Set("ctrl+shift+w", ShortcutCommand.MediaStop);

            Set("ctrl+shift+f", ShortcutCommand.SearchChats);
            Set("ctrl+e", ShortcutCommand.SearchChats);
            Set("ctrl+f", ShortcutCommand.Search);
            Set("search", ShortcutCommand.Search);

            Set("ctrl+pgdown", ShortcutCommand.ChatNext);
            Set("alt+down", ShortcutCommand.ChatNext);
            Set("ctrl+pgup", ShortcutCommand.ChatPrevious);
            Set("alt+up", ShortcutCommand.ChatPrevious);

            Set("ctrl+tab", ShortcutCommand.ChatRecentNext);
            Set("ctrl+shift+tab", ShortcutCommand.ChatRecentPrevious);

            Set("ctrl+alt+home", ShortcutCommand.ChatFirst);
            Set("ctrl+alt+end", ShortcutCommand.ChatLast);

            Set("ctrl+1", ShortcutCommand.ChatPinned1);
            Set("ctrl+2", ShortcutCommand.ChatPinned2);
            Set("ctrl+3", ShortcutCommand.ChatPinned3);
            Set("ctrl+4", ShortcutCommand.ChatPinned4);
            Set("ctrl+5", ShortcutCommand.ChatPinned5);

            for (int i = 0; i < _foldersCommands.Length; i++)
            {
                Set($"ctrl+{i + 1}", _foldersCommands[i]);
            }

            Set("ctrl+shift+down", ShortcutCommand.FolderNext);
            Set("ctrl+shift+up", ShortcutCommand.FolderPrevious);

            Set("ctrl+0", ShortcutCommand.ChatSelf);

            Set("ctrl+9", ShortcutCommand.ShowArchive);

            Set("ctrl+shift+y", ShortcutCommand.SetStatus);
            Set("ctrl+j", ShortcutCommand.Downloads);

            Set("ctrl+home", ShortcutCommand.CallAccept);
            Set("ctrl+end", ShortcutCommand.CallReject);
            Set("ctrl+pgdown", ShortcutCommand.CallToggleMicrophone);
            Set("ctrl+pgup", ShortcutCommand.CallToggleCamera);
        }

        private async Task InitializeCustomAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync("shortcuts.json") as StorageFile;
                if (file == null)
                {
                    return;
                }

                var text = await FileIO.ReadTextAsync(file);

                if (JsonArray.TryParse(text, out JsonArray commands))
                {
                    ApplyCustom(commands);
                }
            }
            catch
            {
                // Nothing here is ours: the file is in LocalState where anyone can edit it, and the
                // defaults are already in the table, so a bad one costs the customisations rather
                // than the launch.
            }
        }

        public async Task<ShortcutsImportResult> ImportAsync(string text)
        {
            if (!JsonArray.TryParse(text, out JsonArray commands))
            {
                return ShortcutsImportResult.Invalid;
            }

            var result = ApplyCustom(commands);
            await SaveCustomAsync();

            return result;
        }

        /// <summary>
        /// Applies a parsed shortcuts file, dropping anything it cannot use.
        /// </summary>
        /// <remarks>
        /// Typed checks rather than the defaulting getters, because <c>GetNamedString</c> and
        /// <c>GetString</c> throw on a value of the wrong type - and a file somebody typed by hand
        /// is exactly where a number turns up where a string belongs.
        /// </remarks>
        private ShortcutsImportResult ApplyCustom(JsonArray commands)
        {
            var applied = 0;
            var ignored = 0;

            var entries = new List<(ShortcutCommand Command, List<Shortcut> Keys)>();

            foreach (var data in commands)
            {
                if (data.ValueType != JsonValueType.Object)
                {
                    ignored++;
                    continue;
                }

                var item = data.GetObject();

                if (!TryGetString(item, "command", out var name) || !_commandByName.TryGetValue(name, out var command))
                {
                    ignored++;
                    continue;
                }

                if (!item.ContainsKey("keys"))
                {
                    ignored++;
                    continue;
                }

                var keys = item.GetNamedValue("keys");
                var parsed = new List<Shortcut>();

                if (keys.ValueType == JsonValueType.Array)
                {
                    foreach (var entry in keys.GetArray())
                    {
                        if (entry.ValueType != JsonValueType.String)
                        {
                            ignored++;
                            continue;
                        }

                        var shortcut = ParseKeys(entry.GetString());
                        if (shortcut == null)
                        {
                            ignored++;
                            continue;
                        }

                        parsed.Add(shortcut);
                    }
                }
                else if (keys.ValueType == JsonValueType.String)
                {
                    // The older form, read as "put this command on this chord".
                    var shortcut = ParseKeys(keys.GetString());
                    if (shortcut == null)
                    {
                        ignored++;
                        continue;
                    }

                    parsed.Add(shortcut);
                }
                else
                {
                    ignored++;
                    continue;
                }

                entries.Add((command, parsed));
            }

            // Two passes. A file that swaps two commands' chords is self-consistent, but applied
            // one entry at a time the first would collide with the binding the second is about to
            // give up - so every command named here lets go of its chords before any is claimed.
            foreach (var entry in entries)
            {
                Remove(entry.Command);
            }

            foreach (var entry in entries)
            {
                foreach (var shortcut in entry.Keys)
                {
                    // An empty list is a deliberate unbind and leaves the command with nothing.
                    // Anything the editor would refuse is refused here too, so a file cannot do
                    // what the UI will not.
                    if (GetConflict(shortcut, entry.Command).Kind != ShortcutConflictKind.None)
                    {
                        ignored++;
                        continue;
                    }

                    Add(shortcut, entry.Command);
                    applied++;
                }
            }

            return new ShortcutsImportResult(true, applied, ignored);
        }

        private static bool TryGetString(JsonObject item, string name, out string value)
        {
            if (item.ContainsKey(name))
            {
                var entry = item.GetNamedValue(name);
                if (entry.ValueType == JsonValueType.String)
                {
                    value = entry.GetString();
                    return value.Length > 0;
                }
            }

            value = null;
            return false;
        }

        private void Set(string keys, ShortcutCommand command, bool replace = false)
        {
            var shortcut = ParseKeys(keys);
            if (shortcut == null)
            {
                return;
            }

            Set(shortcut, command, replace);
        }

        private void Set(Shortcut shortcut, ShortcutCommand command, bool replace = false)
        {
            // Replacing means "this command now answers to this chord", not "this chord now means
            // only this command": wiping the entry would take ShowFolder1 off ctrl+2 along with
            // ChatPinned2.
            if (replace)
            {
                Remove(command);
            }

            Add(shortcut, command);
        }

        private Shortcut ParseKeys(string keys)
        {
            var split = keys.Split('+');

            if (int.TryParse(split[^1], out int number))
            {
                split[^1] = $"number{number}";
            }
            else if (string.Equals(split[^1], "pgdown", StringComparison.OrdinalIgnoreCase))
            {
                split[^1] = "pagedown";
            }
            else if (string.Equals(split[^1], "pgup", StringComparison.OrdinalIgnoreCase))
            {
                split[^1] = "pageup";
            }

            if (Enum.TryParse(split[^1], true, out VirtualKey result))
            {
                var modifiers = VirtualKeyModifiers.None;
                var key = result;

                for (int i = 0; i < split.Length - 1; i++)
                {
                    if (string.Equals(split[i], "ctrl", StringComparison.OrdinalIgnoreCase))
                    {
                        split[i] = "control";
                    }
                    else if (string.Equals(split[i], "alt", StringComparison.OrdinalIgnoreCase))
                    {
                        split[i] = "menu";
                    }

                    if (Enum.TryParse(split[i], true, out VirtualKeyModifiers modifier))
                    {
                        modifiers |= modifier;
                    }
                }

                //if (modifiers == VirtualKeyModifiers.None)
                //{
                //    return null;
                //}

                return new Shortcut(modifiers, key);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Writes the bindings that differ from the defaults, and nothing else.
        /// </summary>
        /// <remarks>
        /// Storing the whole table would freeze today's defaults for anyone who ever opens the
        /// settings page, so a later version could never change one for them.
        /// </remarks>
        public string Export()
        {
            return BuildCustom().Stringify();
        }

        private async Task SaveCustomAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("shortcuts.json", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, BuildCustom().Stringify());
            }
            catch
            {
                // The bindings are already live; only the record of them is lost.
            }
        }

        private JsonArray BuildCustom()
        {
            var array = new JsonArray();

            foreach (var pair in _commandNames)
            {
                var current = GetBindings(pair.Key, _commands);
                var initial = GetBindings(pair.Key, _defaults);

                if (SameBindings(current, initial))
                {
                    continue;
                }

                var keys = new JsonArray();

                foreach (var shortcut in current)
                {
                    keys.Add(JsonValue.CreateStringValue(SerializeKeys(shortcut)));
                }

                array.Add(new JsonObject
                {
                    ["command"] = JsonValue.CreateStringValue(pair.Value),
                    ["keys"] = keys
                });
            }

            return array;
        }

        // The inverse of ParseKeys, which is what has to read this back.
        private static string SerializeKeys(Shortcut shortcut)
        {
            var builder = new StringBuilder();

            if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Control))
            {
                builder.Append("ctrl+");
            }
            if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Menu))
            {
                builder.Append("alt+");
            }
            if (shortcut.Modifiers.HasFlag(VirtualKeyModifiers.Shift))
            {
                builder.Append("shift+");
            }

            if (shortcut.Key is >= VirtualKey.Number0 and <= VirtualKey.Number9)
            {
                builder.Append((int)(shortcut.Key - VirtualKey.Number0));
            }
            else
            {
                builder.Append(shortcut.Key.ToString().ToLowerInvariant());
            }

            return builder.ToString();
        }

        private Shortcut ParseMediaKeys(string keys)
        {
            switch (keys.ToLower())
            {
                case "media play":
                case "media pause":
                case "toggle media play/pause":
                case "media stop":
                case "media previous":
                case "media next":
                    break;
            }

            return null;
        }

        /// <summary>
        /// What a command is called in the shortcuts list.
        /// </summary>
        /// <remarks>
        /// A switch rather than a table so only the one string asked for is realized, and so a
        /// command added to the enum without a name here fails to compile rather than showing up
        /// as its own identifier.
        /// </remarks>
        public static string GetLabel(ShortcutCommand command)
        {
            return command switch
            {
                ShortcutCommand.Search => Strings.ShortcutCommandSearch,
                ShortcutCommand.SearchChats => Strings.ShortcutCommandSearchChats,
                ShortcutCommand.Downloads => Strings.ShortcutCommandDownloads,
                ShortcutCommand.SetStatus => Strings.ShortcutCommandSetStatus,
                ShortcutCommand.MediaPlayPause => Strings.ShortcutCommandMediaPlayPause,
                ShortcutCommand.MediaPrevious => Strings.ShortcutCommandMediaPrevious,
                ShortcutCommand.MediaNext => Strings.ShortcutCommandMediaNext,
                ShortcutCommand.MediaStop => Strings.ShortcutCommandMediaStop,
                ShortcutCommand.Lock => Strings.ShortcutCommandLock,
                ShortcutCommand.Minimize => Strings.ShortcutCommandMinimize,
                ShortcutCommand.Close => Strings.ShortcutCommandClose,
                ShortcutCommand.Quit => Strings.ShortcutCommandQuit,

                ShortcutCommand.ChatPrevious => Strings.ShortcutCommandChatPrevious,
                ShortcutCommand.ChatNext => Strings.ShortcutCommandChatNext,
                ShortcutCommand.ChatRecentPrevious => Strings.ShortcutCommandChatRecentPrevious,
                ShortcutCommand.ChatRecentNext => Strings.ShortcutCommandChatRecentNext,
                ShortcutCommand.ChatFirst => Strings.ShortcutCommandChatFirst,
                ShortcutCommand.ChatLast => Strings.ShortcutCommandChatLast,
                ShortcutCommand.ChatSelf => Strings.ShortcutCommandChatSelf,
                ShortcutCommand.ChatPinned1 => Strings.ShortcutCommandChatPinned1,
                ShortcutCommand.ChatPinned2 => Strings.ShortcutCommandChatPinned2,
                ShortcutCommand.ChatPinned3 => Strings.ShortcutCommandChatPinned3,
                ShortcutCommand.ChatPinned4 => Strings.ShortcutCommandChatPinned4,
                ShortcutCommand.ChatPinned5 => Strings.ShortcutCommandChatPinned5,
                ShortcutCommand.JustSendMessage => Strings.ShortcutCommandJustSendMessage,
                ShortcutCommand.SendSilentMessage => Strings.ShortcutCommandSendSilentMessage,
                ShortcutCommand.ScheduleMessage => Strings.ShortcutCommandScheduleMessage,

                ShortcutCommand.FolderPrevious => Strings.ShortcutCommandFolderPrevious,
                ShortcutCommand.FolderNext => Strings.ShortcutCommandFolderNext,
                ShortcutCommand.ShowAllChats => Strings.ShortcutCommandShowAllChats,
                ShortcutCommand.ShowFolder1 => Strings.ShortcutCommandShowFolder1,
                ShortcutCommand.ShowFolder2 => Strings.ShortcutCommandShowFolder2,
                ShortcutCommand.ShowFolder3 => Strings.ShortcutCommandShowFolder3,
                ShortcutCommand.ShowFolder4 => Strings.ShortcutCommandShowFolder4,
                ShortcutCommand.ShowFolder5 => Strings.ShortcutCommandShowFolder5,
                ShortcutCommand.ShowFolder6 => Strings.ShortcutCommandShowFolder6,
                ShortcutCommand.ShowFolderLast => Strings.ShortcutCommandShowFolderLast,
                ShortcutCommand.ShowArchive => Strings.ShortcutCommandShowArchive,

                ShortcutCommand.CallAccept => Strings.ShortcutCommandCallAccept,
                ShortcutCommand.CallReject => Strings.ShortcutCommandCallReject,
                ShortcutCommand.CallToggleMicrophone => Strings.ShortcutCommandCallToggleMicrophone,
                ShortcutCommand.CallToggleCamera => Strings.ShortcutCommandCallToggleCamera,

                _ => command.ToString()
            };
        }

        public static string GetStringRepresentation(VirtualKey key, VirtualKeyModifiers modifiers = VirtualKeyModifiers.None)
        {
            var builder = new StringBuilder();

            static void ConcatVirtualKey(VirtualKey key, StringBuilder builder)
            {
                if (builder.Length > 0)
                {
                    builder.Append("+");
                }

                builder.Append(key switch
                {
                    VirtualKey.Control => Strings.VirtualKeyModifiersControl,
                    VirtualKey.Menu => Strings.VirtualKeyModifiersMenu,
                    VirtualKey.Shift => Strings.VirtualKeyModifiersShift,
                    (VirtualKey)190 => '.',
                    _ => key.ToString()
                });
            }

            if ((modifiers & VirtualKeyModifiers.Control) != 0)
            {
                ConcatVirtualKey(VirtualKey.Control, builder);
            }

            if ((modifiers & VirtualKeyModifiers.Menu) != 0)
            {
                ConcatVirtualKey(VirtualKey.Menu, builder);
            }

            if ((modifiers & VirtualKeyModifiers.Shift) != 0)
            {
                ConcatVirtualKey(VirtualKey.Shift, builder);
            }

            ConcatVirtualKey(key, builder);
            return builder.ToString();
        }
    }

    public enum ShortcutContext
    {
        Global,
        Call,
        PinnedChats,
        Folders,
        Chat,
    }

    public enum ShortcutOwner
    {
        MessageEditor,
        TextEditing,
        Navigation,
    }

    public enum ShortcutConflictKind
    {
        None,
        Reserved,
        Command,
    }

    public partial class ShortcutConflict
    {
        public static readonly ShortcutConflict None = new();

        private ShortcutConflict()
        {
            Kind = ShortcutConflictKind.None;
        }

        public ShortcutConflict(ShortcutOwner owner)
        {
            Kind = ShortcutConflictKind.Reserved;
            Owner = owner;
        }

        public ShortcutConflict(ShortcutCommand command)
        {
            Kind = ShortcutConflictKind.Command;
            Command = command;
        }

        public ShortcutConflictKind Kind { get; }

        public ShortcutOwner Owner { get; }

        public ShortcutCommand Command { get; }
    }

    public sealed partial class ShortcutList : KeyedList<string, ShortcutInfo>
    {
        public ShortcutList(string key)
            : base(key)
        {
        }
    }

    public sealed partial class ShortcutInfo : BindableBase
    {
        public ShortcutInfo(Shortcut shortcut, ShortcutCommand command)
        {
            Shortcut = shortcut;
            Command = command;
        }

        public ShortcutCommand Command { get; private set; }

        private Shortcut _shortcut;
        public Shortcut Shortcut
        {
            get => _shortcut;
            set
            {
                if (Set(ref _shortcut, value))
                {
                    _compact = null;
                    RaisePropertyChanged(nameof(IsUnbound));
                }
            }
        }

        /// <summary>
        /// Whether this row is listening for a chord.
        /// </summary>
        /// <remarks>
        /// Row state rather than presentation: only one row holds it at a time, and the page
        /// decides what it looks like.
        /// </remarks>
        private bool _isRecording;
        public bool IsRecording
        {
            get => _isRecording;
            set => Set(ref _isRecording, value);
        }

        /// <summary>
        /// Why the last chord was refused, as the service reported it, or
        /// <see cref="ShortcutConflict.None"/>.
        /// </summary>
        /// <remarks>
        /// Never null, and that is load-bearing rather than tidiness: x:Bind skips a function
        /// binding whose argument comes back null and leaves the target showing what it had, so a
        /// refusal cleared to null stayed on screen for the life of the row. Turning this into a
        /// sentence is still the page's business.
        /// </remarks>
        private ShortcutConflict _conflict = ShortcutConflict.None;
        public ShortcutConflict Conflict
        {
            get => _conflict;
            set => Set(ref _conflict, value ?? ShortcutConflict.None);
        }

        // Same trap: the placeholder has to be driven by something that is never null, and it is
        // wanted exactly when Shortcut is.
        public bool IsUnbound => _shortcut == null;

        /// <summary>
        /// Whether this row answers a search for <paramref name="query"/>.
        /// </summary>
        /// <param name="compact">
        /// The same query reduced by <see cref="Compact"/>, passed in rather than recomputed so a
        /// keystroke reduces it once instead of once per row.
        /// </param>
        public bool Matches(string query, string compact)
        {
            if (ShortcutsService.GetLabel(Command).Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }

            if (_shortcut == null || compact.Length == 0)
            {
                return false;
            }

            _compact ??= Compact(_shortcut.ToString());
            return _compact.Contains(compact, StringComparison.Ordinal);
        }

        private string _compact;

        /// <summary>
        /// Lowercases and drops everything that isn't a letter or a digit.
        /// </summary>
        /// <remarks>
        /// So that "ctrl+shift+w", "ctrl shift w" and "ctrlshiftw" all find the row the list
        /// draws as Ctrl+Shift+W. It works on what the row shows, so a localized modifier name is
        /// what the user types.
        /// </remarks>
        public static string Compact(string value)
        {
            var builder = new StringBuilder(value.Length);

            foreach (var character in value)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLower(character));
                }
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return $"{{ {Command}, {_shortcut} }}";
        }
    }

    public partial class Shortcut
    {
        public VirtualKeyModifiers Modifiers { get; }
        public VirtualKey Key { get; }

        private string[] _components;

        // Realized on demand: only the settings page reads them, and every modifier name behind
        // them is a resource lookup.
        public string[] Components => _components ??= GetComponents();

        public Shortcut(VirtualKeyModifiers modifiers, VirtualKey key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public override bool Equals(object obj)
        {
            if (obj is Shortcut shortcut)
            {
                return shortcut.Modifiers == Modifiers
                    && shortcut.Key == Key;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Modifiers, Key);
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            foreach (var key in GetComponents())
            {
                if (builder.Length > 0)
                {
                    builder.Append("+");
                }

                builder.Append(key);
            }

            return builder.ToString();
        }

        private string[] GetComponents()
        {
            var parts = new List<string>();

            if (Modifiers.HasFlag(VirtualKeyModifiers.Control))
            {
                parts.Add(Strings.VirtualKeyModifiersControl);
            }
            if (Modifiers.HasFlag(VirtualKeyModifiers.Menu))
            {
                parts.Add(Strings.VirtualKeyModifiersMenu);
            }
            if (Modifiers.HasFlag(VirtualKeyModifiers.Shift))
            {
                parts.Add(Strings.VirtualKeyModifiersShift);
            }

            if (Key is >= VirtualKey.Number0 and <= VirtualKey.Number9)
            {
                parts.Add($"{Key - VirtualKey.Number0}");
            }
            else
            {
                parts.Add(Key.ToString());
            }

            return parts.ToArray();
        }
    }

    public enum ShortcutCommand
    {
        Close,
        Lock,
        Minimize,
        Quit,

        //MediaPlay,
        //MediaPause,
        MediaPlayPause,
        MediaStop,
        MediaPrevious,
        MediaNext,

        Search,
        SearchChats,

        ChatPrevious,
        ChatNext,
        ChatRecentPrevious,
        ChatRecentNext,
        ChatFirst,
        ChatLast,
        ChatSelf,
        ChatPinned1,
        ChatPinned2,
        ChatPinned3,
        ChatPinned4,
        ChatPinned5,

        ShowAllChats,
        ShowFolder1,
        ShowFolder2,
        ShowFolder3,
        ShowFolder4,
        ShowFolder5,
        ShowFolder6,
        ShowFolderLast,

        FolderNext,
        FolderPrevious,

        ShowArchive,

        JustSendMessage,
        SendSilentMessage,
        ScheduleMessage,

        SetStatus,
        Downloads,

        CallAccept,
        CallReject,
        CallToggleMicrophone,
        CallToggleCamera,

        //SupportReloadTemplates,
        //SupportToggleMuted,
        //SupportScrollToCurrent,
        //SupportHistoryBack,
        //SupportHistoryForward,
    }
}
