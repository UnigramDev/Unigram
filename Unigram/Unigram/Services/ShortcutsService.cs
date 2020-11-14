using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unigram.Collections;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Data.Json;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Unigram.Services
{
    public interface IShortcutsService
    {
        InvokedShortcut Process(AcceleratorKeyEventArgs args);

        bool TryGetShortcut(AcceleratorKeyEventArgs args, out Shortcut shortcut);

        IList<ShortcutList> GetShortcuts();
        IList<ShortcutList> Update(Shortcut shortcut, ShortcutCommand command);
    }

    public class InvokedShortcut : Shortcut
    {
        public IList<ShortcutCommand> Commands { get; }

        public InvokedShortcut(VirtualKeyModifiers modifiers, VirtualKey key, IList<ShortcutCommand> commands)
            : base(modifiers, key)
        {
            Commands = commands;
        }
    }

    public class ShortcutsService : TLViewModelBase, IShortcutsService
    {
        #region Const

        private readonly ShortcutCommand[] _autoRepeatCommands = new[]
        {
            //ShortcutCommand.MediaPrevious,
            //ShortcutCommand.MediaNext,
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

        private readonly Dictionary<string, ShortcutCommand> _commandByName = new Dictionary<string, ShortcutCommand>
        {
            { "close_telegram"    , ShortcutCommand.Close },
            { "lock_telegram"     , ShortcutCommand.Lock },
            { "minimize_telegram" , ShortcutCommand.Minimize },
            { "quit_telegram"     , ShortcutCommand.Quit },

            //{ "media_play"        , ShortcutCommand.MediaPlay },
            //{ "media_pause"       , ShortcutCommand.MediaPause },
            //{ "media_playpause"   , ShortcutCommand.MediaPlayPause },
            //{ "media_stop"        , ShortcutCommand.MediaStop },
            //{ "media_previous"    , ShortcutCommand.MediaPrevious },
            //{ "media_next"        , ShortcutCommand.MediaNext },

            { "search"            , ShortcutCommand.Search },

            { "previous_chat"     , ShortcutCommand.ChatPrevious },
            { "next_chat"         , ShortcutCommand.ChatNext },
            { "first_chat"        , ShortcutCommand.ChatFirst },
            { "last_chat"         , ShortcutCommand.ChatLast },
            { "self_chat"         , ShortcutCommand.ChatSelf },

            { "previous_folder"   , ShortcutCommand.FolderPrevious },
            { "next_folder"       , ShortcutCommand.FolderNext },
            { "all_chats"         , ShortcutCommand.ShowAllChats },

            { "folder1"           , ShortcutCommand.ShowFolder1 },
            { "folder2"           , ShortcutCommand.ShowFolder2 },
            { "folder3"           , ShortcutCommand.ShowFolder3 },
            { "folder4"           , ShortcutCommand.ShowFolder4 },
            { "folder5"           , ShortcutCommand.ShowFolder5 },
            { "folder6"           , ShortcutCommand.ShowFolder6 },
            { "last_folder"       , ShortcutCommand.ShowFolderLast },

            { "show_archive"      , ShortcutCommand.ShowArchive },

			// Shortcuts that have no default values.
			{ "message"           , ShortcutCommand.JustSendMessage },
            { "message_silently"  , ShortcutCommand.SendSilentMessage },
            { "message_scheduled" , ShortcutCommand.ScheduleMessage },
			//
		};

        private readonly Dictionary<ShortcutCommand, string> _commandNames = new Dictionary<ShortcutCommand, string>
        {
            { ShortcutCommand.Close          , "close_telegram" },
            { ShortcutCommand.Lock           , "lock_telegram" },
            { ShortcutCommand.Minimize       , "minimize_telegram" },
            { ShortcutCommand.Quit           , "quit_telegram" },

            //{ ShortcutCommand.MediaPlay      , "media_play" },
            //{ ShortcutCommand.MediaPause     , "media_pause" },
            //{ ShortcutCommand.MediaPlayPause , "media_playpause" },
            //{ ShortcutCommand.MediaStop      , "media_stop" },
            //{ ShortcutCommand.MediaPrevious  , "media_previous" },
            //{ ShortcutCommand.MediaNext      , "media_next" },

            { ShortcutCommand.Search         , "search" },

            { ShortcutCommand.ChatPrevious   , "previous_chat" },
            { ShortcutCommand.ChatNext       , "next_chat" },
            { ShortcutCommand.ChatFirst      , "first_chat" },
            { ShortcutCommand.ChatLast       , "last_chat" },
            { ShortcutCommand.ChatSelf       , "self_chat" },

            { ShortcutCommand.FolderPrevious , "previous_folder" },
            { ShortcutCommand.FolderNext     , "next_folder" },
            { ShortcutCommand.ShowAllChats   , "all_chats" },

            { ShortcutCommand.ShowFolder1    , "folder1" },
            { ShortcutCommand.ShowFolder2    , "folder2" },
            { ShortcutCommand.ShowFolder3    , "folder3" },
            { ShortcutCommand.ShowFolder4    , "folder4" },
            { ShortcutCommand.ShowFolder5    , "folder5" },
            { ShortcutCommand.ShowFolder6    , "folder6" },
            { ShortcutCommand.ShowFolderLast , "last_folder" },

            { ShortcutCommand.ShowArchive    , "show_archive" },
        };

        #endregion

        private readonly Dictionary<Shortcut, List<ShortcutCommand>> _commands = new Dictionary<Shortcut, List<ShortcutCommand>>();

        public ShortcutsService(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            InitializeDefault();
            InitializeCustom();
        }

        public InvokedShortcut Process(AcceleratorKeyEventArgs args)
        {
            if (args.EventType != CoreAcceleratorKeyEventType.KeyDown && args.EventType != CoreAcceleratorKeyEventType.SystemKeyDown)
            {
                return new InvokedShortcut(VirtualKeyModifiers.None, VirtualKey.None, new ShortcutCommand[0]);
            }

            var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            var modifiers = VirtualKeyModifiers.None;
            if (alt)
            {
                modifiers |= VirtualKeyModifiers.Menu;
            }
            if (ctrl)
            {
                modifiers |= VirtualKeyModifiers.Control;
            }
            if (shift)
            {
                modifiers |= VirtualKeyModifiers.Shift;
            }

            var shortcut = new Shortcut(modifiers, args.VirtualKey);
            if (_commands.TryGetValue(shortcut, out var value))
            {
                return new InvokedShortcut(modifiers, args.VirtualKey, value);
            }

            return new InvokedShortcut(VirtualKeyModifiers.None, VirtualKey.None, new ShortcutCommand[0]);
        }

        //[DllImport("user32.dll")]
        //static extern int MapVirtualKey(uint uCode, uint uMapType);

        //int nonVirtualKey = MapVirtualKey((uint)args.VirtualKey, 2);
        //char mappedChar = Convert.ToChar(nonVirtualKey);

        public bool TryGetShortcut(AcceleratorKeyEventArgs args, out Shortcut shortcut)
        {
            if (args.EventType != CoreAcceleratorKeyEventType.KeyDown && args.EventType != CoreAcceleratorKeyEventType.SystemKeyDown)
            {
                shortcut = null;
                return false;
            }

            var alt = Window.Current.CoreWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down);
            var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            var shift = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            var modifiers = VirtualKeyModifiers.None;
            if (alt)
            {
                modifiers |= VirtualKeyModifiers.Menu;
            }
            if (ctrl)
            {
                modifiers |= VirtualKeyModifiers.Control;
            }
            if (shift)
            {
                modifiers |= VirtualKeyModifiers.Shift;
            }

            return TryGetShortcut(modifiers, args.VirtualKey, out shortcut);
        }

        private bool TryGetShortcut(VirtualKeyModifiers modifiers, VirtualKey key, out Shortcut shortcut)
        {
            if (key == VirtualKey.Control || key == VirtualKey.LeftControl || key == VirtualKey.RightControl)
            {
                shortcut = null;
                return false;
            }
            else if (key == VirtualKey.Menu || key == VirtualKey.LeftMenu || key == VirtualKey.RightMenu)
            {
                shortcut = null;
                return false;
            }
            else if (key == VirtualKey.Shift || key == VirtualKey.LeftShift || key == VirtualKey.RightShift)
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
            var temp = new List<ShortcutInfo>();
            var result = new List<ShortcutList>();

            foreach (var commands in _commands)
            {
                foreach (var value in commands.Value)
                {
                    if (IsSpecialShortcut(commands.Key, value))
                    {
                        continue;
                    }

                    temp.Add(new ShortcutInfo(commands.Key, value));
                }
            }

            var categories = new Dictionary<string, IList<ShortcutCommand>>
            {
                {
                    "App", new[]
                    {
                        ShortcutCommand.Close          ,
                        ShortcutCommand.Lock           ,
                        ShortcutCommand.Minimize       ,
                        ShortcutCommand.Quit           ,
                        ShortcutCommand.Search
                    }
                },

                //{ ShortcutCommand.MediaPlay      , "media_play" },
                //{ ShortcutCommand.MediaPause     , "media_pause" },
                //{ ShortcutCommand.MediaPlayPause , "media_playpause" },
                //{ ShortcutCommand.MediaStop      , "media_stop" },
                //{ ShortcutCommand.MediaPrevious  , "media_previous" },
                //{ ShortcutCommand.MediaNext      , "media_next" },
                //{
                //    "App", new[] { ShortcutCommand.Search }
                //},
                {
                    "Chats", new[]
                    {
                        ShortcutCommand.ChatPrevious   ,
                        ShortcutCommand.ChatNext       ,
                        ShortcutCommand.ChatFirst      ,
                        ShortcutCommand.ChatLast       ,
                        ShortcutCommand.ChatSelf       ,
                    }
                },
                {
                    "Folders", new[]
                    {
                        ShortcutCommand.FolderPrevious ,
                        ShortcutCommand.FolderNext     ,
                        ShortcutCommand.ShowAllChats   ,
                        ShortcutCommand.ShowFolder1    ,
                        ShortcutCommand.ShowFolder2    ,
                        ShortcutCommand.ShowFolder3    ,
                        ShortcutCommand.ShowFolder4    ,
                        ShortcutCommand.ShowFolder5    ,
                        ShortcutCommand.ShowFolder6    ,
                        ShortcutCommand.ShowFolderLast ,
                        ShortcutCommand.ShowArchive    ,
                    }
                }
            };

            foreach (var category in categories)
            {
                var items = new ShortcutList(category.Key);

                foreach (var command in category.Value)
                {
                    var item = temp.FirstOrDefault(x => x.Command == command);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }

                if (items.Count > 0)
                {
                    result.Add(items);
                }
            }

            return result;
        }

        public IList<ShortcutList> Update(Shortcut shortcut, ShortcutCommand command)
        {
            Set(shortcut, command, true);
            return GetShortcuts();
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

            Set("ctrl+f", ShortcutCommand.Search);
            Set("search", ShortcutCommand.Search);

            Set("ctrl+pgdown", ShortcutCommand.ChatNext);
            Set("alt+down", ShortcutCommand.ChatNext);
            Set("ctrl+pgup", ShortcutCommand.ChatPrevious);
            Set("alt+up", ShortcutCommand.ChatPrevious);

            Set("ctrl+tab", ShortcutCommand.ChatNext);
            Set("ctrl+shift+tab", ShortcutCommand.ChatPrevious);

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
        }

        private async void InitializeCustom()
        {
            var file = await ApplicationData.Current.LocalFolder.TryGetItemAsync("shortcuts.json") as StorageFile;
            if (file == null)
            {
                return;
            }

            var text = await FileIO.ReadTextAsync(file);

            if (JsonArray.TryParse(text, out JsonArray commands))
            {
                foreach (var data in commands)
                {
                    if (data.ValueType != JsonValueType.Object)
                    {
                        continue;
                    }

                    var item = data.GetObject();
                    if (item.ContainsKey("keys") && item.ContainsKey("command"))
                    {
                        var keys = item.GetNamedString("keys", string.Empty);
                        var command = item.GetNamedString("command", string.Empty);

                        if (string.IsNullOrEmpty(keys) || string.IsNullOrEmpty(command))
                        {
                            continue;
                        }

                        if (_commandByName.TryGetValue(command, out ShortcutCommand value))
                        {
                            Set(keys, value, true);
                        }
                    }
                }
            }
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
            List<ShortcutCommand> commands;
            if (_commands.ContainsKey(shortcut))
            {
                if (replace)
                {
                    commands = _commands[shortcut] = new List<ShortcutCommand>();
                }
                else
                {
                    commands = _commands[shortcut];
                }
            }
            else
            {
                commands = _commands[shortcut] = new List<ShortcutCommand>();
            }

            commands.Add(command);
        }

        private Shortcut ParseKeys(string keys)
        {
            var split = keys.Split('+');

            if (int.TryParse(split[split.Length - 1], out int number))
            {
                split[split.Length - 1] = $"number{number}";
            }
            else if (string.Equals(split[split.Length - 1], "pgdown", StringComparison.OrdinalIgnoreCase))
            {
                split[split.Length - 1] = "pagedown";
            }
            else if (string.Equals(split[split.Length - 1], "pgup", StringComparison.OrdinalIgnoreCase))
            {
                split[split.Length - 1] = "pageup";
            }

            if (Enum.TryParse(split[split.Length - 1], true, out VirtualKey result))
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
    }

    public sealed class ShortcutList : KeyedList<string, ShortcutInfo>
    {
        public ShortcutList(string key)
            : base(key)
        {
        }
    }

    public sealed class ShortcutInfo : BindableBase
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
            set => Set(ref _shortcut, value);
        }

        public override string ToString()
        {
            return $"{{ {Command}, {_shortcut} }}";
        }
    }

    public class Shortcut
    {
        public VirtualKeyModifiers Modifiers { get; }
        public VirtualKey Key { get; }

        public string[] Components { get; }

        public Shortcut(VirtualKeyModifiers modifiers, VirtualKey key)
        {
            Modifiers = modifiers;
            Key = key;

            Components = GetComponents();
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
            return Modifiers.GetHashCode()
                ^ Key.GetHashCode();
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            foreach (var key in Components)
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
            var modifiers = Enum.GetValues(typeof(VirtualKeyModifiers))
                .Cast<VirtualKeyModifiers>()
                .Where(v => v != VirtualKeyModifiers.None && Modifiers.HasFlag(v));

            foreach (var key in modifiers)
            {
                switch (key)
                {
                    case VirtualKeyModifiers.Control:
                        parts.Add("Ctrl");
                        break;
                    case VirtualKeyModifiers.Menu:
                        parts.Add("Alt");
                        break;
                    case VirtualKeyModifiers.Shift:
                        parts.Add("Shift");
                        break;
                }
            }

            if (Key >= VirtualKey.Number0 && Key <= VirtualKey.Number9)
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
        //MediaPlayPause,
        //MediaStop,
        //MediaPrevious,
        //MediaNext,

        Search,

        ChatPrevious,
        ChatNext,
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

        //SupportReloadTemplates,
        //SupportToggleMuted,
        //SupportScrollToCurrent,
        //SupportHistoryBack,
        //SupportHistoryForward,
    }
}
