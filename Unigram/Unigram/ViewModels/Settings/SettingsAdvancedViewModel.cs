using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Services.Updates;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsAdvancedViewModel : TLViewModelBase, IHandle<UpdateFile>, IHandle<UpdateAppVersion>
    {
        private readonly ICloudUpdateService _cloudUpdateService;
        private CloudUpdate _update;

        public SettingsAdvancedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ICloudUpdateService cloudUpdateService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _cloudUpdateService = cloudUpdateService;

            UpdateCommand = new RelayCommand(UpdateExecute);

        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            _update = _cloudUpdateService.NextUpdate;
            UpdateExecute();

            Aggregator.Subscribe(this);
            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(pageState, suspending);
        }

        #region Updates

        private bool _isUpdateEnabled = true;
        public bool IsUpdateEnabled
        {
            get => _isUpdateEnabled;
            set => Set(ref _isUpdateEnabled, value);
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

        public void Handle(UpdateFile update)
        {
            if (_update.UpdateFile(update.File))
            {
                BeginOnUIThread(() => Update(_update));
            }
        }

        public void Handle(UpdateAppVersion update)
        {
            BeginOnUIThread(() => Update(_update = update.Update));
        }

        private void Update(CloudUpdate update)
        {
            if (update == null)
            {
                IsUpdateEnabled = true;

                UpdateText = "Check for Updates";
                UpdateFooter = "You have the latest version of Unigram.";
            }
            else if (update.File != null)
            {
                // Update is ready to be installed
                IsUpdateEnabled = true;

                UpdateText = Strings.Resources.UpdateTelegram;
                UpdateFooter = "Please update the app to get the latest features and improvements.";
            }
            else if (update.Document.Local.IsDownloadingActive)
            {
                // Update is being downloaded
                IsUpdateEnabled = false;

                UpdateText = string.Format("Downloading... {0} / {1}", FileSizeConverter.Convert(update.Document.Local.DownloadedSize, update.Document.Size), FileSizeConverter.Convert(update.Document.Size));
                UpdateFooter = "Please update the app to get the latest features and improvements.";
            }
            else if (update.Document.Local.CanBeDownloaded)
            {
                ProtoService.DownloadFile(update.Document.Id, 32);
            }
        }

        public RelayCommand UpdateCommand { get; }
        private async void UpdateExecute()
        {
            var update = _update;
            if (update == null)
            {
                IsUpdateEnabled = false;

                UpdateText = "Retrieving Information...";

                var ticks = Environment.TickCount;

                await _cloudUpdateService.UpdateAsync(true);
                update = _update = _cloudUpdateService.NextUpdate;

                var diff = Environment.TickCount - ticks;
                if (diff < 2000)
                {
                    await Task.Delay(2000 - diff);
                }
            }
            else if (update.File != null)
            {
                await Launcher.LaunchFileAsync(update.File);
                Application.Current.Exit();
            }

            Update(update);
        }

        #endregion

        public bool IsAdaptiveWideEnabled
        {
            get
            {
                return Settings.IsAdaptiveWideEnabled;
            }
            set
            {
                Settings.IsAdaptiveWideEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool PreferIpv6
        {
            get
            {
                return CacheService.Options.PreferIpv6;
            }
            set
            {
                CacheService.Options.PreferIpv6 = value;
                RaisePropertyChanged();
            }
        }

        public bool IsTrayVisible
        {
            get { return Settings.IsTrayVisible; }
            set
            {
                if (Settings.IsTrayVisible != value)
                {
                    Settings.IsTrayVisible = value;
                    RaisePropertyChanged();

                    if (App.Connection != null)
                    {
                        App.Connection.SendMessageAsync(new Windows.Foundation.Collections.ValueSet { { "IsTrayVisible", value } });
                    }
                }
            }
        }
    }
}
