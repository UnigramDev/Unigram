//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsShortcutsViewModel : ViewModelBase
    {
        private readonly IShortcutsService _shortcutsService;

        /// <summary>
        /// Every row, unfiltered, and the same instances throughout.
        /// </summary>
        /// <remarks>
        /// An edit updates the row in place rather than rebuilding the list, so the ListView keeps
        /// its containers and the scroll position and focus survive. Only a query changes what is
        /// in <see cref="Items"/>, because only a query changes which rows exist.
        /// </remarks>
        private IList<ShortcutList> _source;

        private string _query = string.Empty;
        private string _compact = string.Empty;

        public SettingsShortcutsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IShortcutsService shortcutsService)
            : base(clientService, settingsService, aggregator)
        {
            _shortcutsService = shortcutsService;

            Items = new RangeObservableCollection<ShortcutList>();
        }

        // shortcuts.json is applied asynchronously, so a snapshot taken in the constructor can be
        // the defaults on an install that has customised them.
        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            await _shortcutsService.InitializeAsync();

            _source = _shortcutsService.GetShortcuts();
            Items.ReplaceWith(_source);
        }

        public RangeObservableCollection<ShortcutList> Items { get; private set; }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            private set => Set(ref _isEmpty, value);
        }

        /// <summary>
        /// Narrows the list to <paramref name="query"/>, and returns how many rows are left.
        /// </summary>
        /// <remarks>
        /// The count goes back to the page rather than onto a property: a filter that changes the
        /// list without saying so is worse than no filter for someone who cannot see it shrink,
        /// and the page is what can announce.
        /// </remarks>
        public int Search(string query)
        {
            _query = query?.Trim() ?? string.Empty;
            _compact = ShortcutInfo.Compact(_query);

            if (_source == null)
            {
                return 0;
            }

            if (_query.Length == 0)
            {
                // The same group instances go back in, so clearing the box restores the list the
                // ListView was already showing rather than a copy of it.
                Items.ReplaceWith(_source);
                IsEmpty = false;

                var total = 0;

                foreach (var group in _source)
                {
                    total += group.Count;
                }

                return total;
            }

            var result = new List<ShortcutList>();
            var count = 0;

            foreach (var group in _source)
            {
                // Built only once the group has a match, so a query that excludes a whole category
                // leaves no empty header behind.
                ShortcutList filtered = null;

                foreach (var info in group)
                {
                    if (info.Matches(_query, _compact))
                    {
                        filtered ??= new ShortcutList(group.Key);
                        filtered.Add(info);
                        count++;
                    }
                }

                if (filtered != null)
                {
                    result.Add(filtered);
                }
            }

            Items.ReplaceWith(result);
            IsEmpty = count == 0;

            return count;
        }

        /// <summary>
        /// The row listening for a chord, or null.
        /// </summary>
        /// <remarks>
        /// Only ever one: recording takes the keyboard away from the whole app, so two rows
        /// listening at once would be two claims on the same keys.
        /// </remarks>
        public ShortcutInfo Recording { get; private set; }

        public void StartRecording(ShortcutInfo info)
        {
            StopRecording();

            Recording = info;

            // Not an early return when it is the same row: clicking one that is already recording
            // means "let me try again", and the refusal it is showing is what has to go.
            info.Conflict = ShortcutConflict.None;
            info.IsRecording = true;
        }

        public void StopRecording()
        {
            if (Recording is ShortcutInfo info)
            {
                Recording = null;

                info.IsRecording = false;
                info.Conflict = ShortcutConflict.None;
            }
        }

        /// <summary>
        /// Applies a captured chord, or leaves the row recording and carrying the refusal.
        /// </summary>
        /// <returns>
        /// Whether the recording ended. False keeps the row listening, so a refused chord costs
        /// nothing but another try.
        /// </returns>
        public async Task<bool> CommitAsync(Shortcut shortcut)
        {
            if (Recording is not ShortcutInfo info)
            {
                return false;
            }

            var conflict = _shortcutsService.GetConflict(shortcut, info.Command);
            if (conflict.Kind != ShortcutConflictKind.None)
            {
                info.Conflict = conflict;
                return false;
            }

            StopRecording();

            await _shortcutsService.UpdateAsync(shortcut, info.Command);
            info.Shortcut = _shortcutsService.GetShortcut(info.Command);

            return true;
        }

        // The page stops the recording before calling either of these: it holds the lease on the
        // keyboard, and clearing the row state here would leave it holding one for a row that is
        // no longer recording.
        public async void Unbind(ShortcutInfo info)
        {
            await _shortcutsService.UnbindAsync(info.Command);
            info.Shortcut = _shortcutsService.GetShortcut(info.Command);
        }

        public async void Reset(ShortcutInfo info)
        {
            await _shortcutsService.ResetAsync(info.Command);
            info.Shortcut = _shortcutsService.GetShortcut(info.Command);
        }

        public async void Export()
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "shortcuts"
            };

            picker.FileTypeChoices.Add("JSON", new[] { ".json" });

            try
            {
                var file = await picker.PickSaveFileAsync(XamlRoot);
                if (file == null)
                {
                    return;
                }

                // FileIO rather than System.IO: a picked file is reachable through the broker that
                // granted it and nothing else.
                await FileIO.WriteTextAsync(file, _shortcutsService.Export());
            }
            catch
            {
                // A cancelled or unwritable picker is not worth a message.
            }
        }

        public async void Import()
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            picker.FileTypeFilter.Add(".json");

            string text;

            try
            {
                var file = await picker.PickSingleFileAsync(XamlRoot);
                if (file == null)
                {
                    return;
                }

                text = await FileIO.ReadTextAsync(file);
            }
            catch
            {
                return;
            }

            StopRecording();

            var result = await _shortcutsService.ImportAsync(text);
            if (!result.Valid)
            {
                ToastPopup.Show(XamlRoot, Strings.ShortcutsImportInvalid, ToastPopupIcon.Error);
                return;
            }

            Refresh();

            var message = Locale.Declension(Strings.R.ShortcutsImported, result.Applied);

            // Said out loud rather than swallowed: an entry that was dropped is the only sign that
            // the file asked for something the editor would not allow either.
            if (result.Ignored > 0)
            {
                message += ", " + Locale.Declension(Strings.R.ShortcutsImportIgnored, result.Ignored);
            }

            ToastPopup.Show(XamlRoot, message);
        }

        public async void ResetAll()
        {
            var confirm = await ShowPopupAsync(Strings.ShortcutsRestoreAllConfirm, Strings.ShortcutsRestoreAll, Strings.OK, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            StopRecording();

            await _shortcutsService.ResetAllAsync();
            Refresh();
        }

        // Every row can have changed, but they are still the same rows, so nothing is rebuilt and
        // the list does not move.
        private void Refresh()
        {
            foreach (var group in _source)
            {
                foreach (var info in group)
                {
                    info.Shortcut = _shortcutsService.GetShortcut(info.Command);
                }
            }
        }
    }
}
