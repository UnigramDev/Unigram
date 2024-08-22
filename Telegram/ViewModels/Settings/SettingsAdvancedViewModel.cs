//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Microsoft.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsAdvancedViewModel : ViewModelBase, IHandle
    {
        private readonly ICloudUpdateService _cloudUpdateService;
        private CloudUpdate _update;

        private long _fileToken;

        public SettingsAdvancedViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ICloudUpdateService cloudUpdateService)
            : base(clientService, settingsService, aggregator)
        {
            _cloudUpdateService = cloudUpdateService;
            UpdateFooter = string.Format(Strings.VersionNumber, VersionLabel.GetVersion());
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            _update = _cloudUpdateService.NextUpdate;
            UpdateImpl(false);

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateAppVersion>(this, Handle);
        }

        protected override void OnNavigatedFrom(NavigationState suspensionState, bool suspending)
        {
            UpdateManager.Unsubscribe(this, ref _fileToken);
        }

        #region Updates

        public bool InstallBetaUpdates
        {
            get => Settings.InstallBetaUpdates;
            set
            {
                Settings.InstallBetaUpdates = value;
                RaisePropertyChanged();
                UpdateImpl(false);
            }
        }

        private bool _isUpdateEnabled = true;
        public bool IsUpdateEnabled
        {
            get => _isUpdateEnabled;
            set => Set(ref _isUpdateEnabled, value);
        }

        private string _updateGlyph;
        public string UpdateGlyph
        {
            get => _updateGlyph;
            set => Set(ref _updateGlyph, value);
        }

        private string _updateText;
        public string UpdateText
        {
            get => _updateText;
            set => Set(ref _updateText, value);
        }

        private string _updateFooter;
        public string UpdateFooter
        {
            get => _updateFooter;
            set => Set(ref _updateFooter, value);
        }

        public void Handle(UpdateAppVersion update)
        {
            BeginOnUIThread(() => UpdateFile(_update = update.Update, update.Update.Document, true));
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_update, file, false);
        }

        private void UpdateFile(CloudUpdate update, File file, bool download)
        {
            if (update == null)
            {
                IsUpdateEnabled = true;

                UpdateGlyph = Icons.ArrowSync;
                UpdateText = Strings.CheckForUpdates;
                UpdateFooter = Strings.CheckForUpdatesInfo;
            }
            else if (update.File != null)
            {
                // Update is ready to be installed
                IsUpdateEnabled = true;

                UpdateGlyph = Icons.ArrowSync;
                UpdateText = Strings.UpdateTelegram;
                UpdateFooter = Strings.UpdateTelegramInfo;
            }
            else if (file.Local.IsDownloadingActive)
            {
                // Update is being downloaded
                IsUpdateEnabled = false;

                UpdateGlyph = Icons.ArrowSync;
                UpdateText = string.Format("{0}... {1} / {2}", Strings.Downloading, FileSizeConverter.Convert(update.Document.Local.DownloadedSize, update.Document.Size), FileSizeConverter.Convert(update.Document.Size));
                UpdateFooter = Strings.UpdateTelegramInfo;
            }
            else if (file.Local.CanBeDownloaded)
            {
                ClientService.DownloadFile(update.Document.Id, 32);
            }

            if (download && file != null)
            {
                UpdateManager.Subscribe(this, ClientService, file, ref _fileToken, UpdateFile);
            }
        }

        public void Update()
        {
            UpdateImpl(true);
        }

        private async void UpdateImpl(bool launch)
        {
            var update = _update;
            if (update == null)
            {
                IsUpdateEnabled = false;

                UpdateGlyph = Icons.ArrowSync;
                UpdateText = Strings.RetrievingInformation;

                var ticks = Logger.TickCount;

                await _cloudUpdateService.UpdateAsync(true);
                update = _update = _cloudUpdateService.NextUpdate;

                var diff = (int)(Logger.TickCount - ticks);
                if (diff < 2000)
                {
                    await Task.Delay(2000 - diff);
                }
            }
            else if (update.File != null && launch && Constants.RELEASE)
            {
                await CloudUpdateService.LaunchAsync(Dispatcher, false);
            }

            UpdateFile(update, update?.Document, true);
        }

        #endregion

        public bool IsTrayVisible
        {
            get => Settings.IsTrayVisible;
            set => SetTrayVisible(value);
        }

        private async void SetTrayVisible(bool value)
        {
            if (Settings.IsTrayVisible == value)
            {
                return;
            }

            Settings.IsTrayVisible = value;
            RaisePropertyChanged();

            if (value)
            {
                await NotifyIcon.LaunchAsync();
            }
            else
            {
                await NotifyIcon.ExitAsync();
            }
        }
    }
}
