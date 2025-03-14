//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.System;

namespace Telegram.Services
{
    public interface ICloudUpdateService
    {
        CloudUpdate NextUpdate { get; }
        Task UpdateAsync(bool force);
    }

    public partial class CloudUpdateService : ICloudUpdateService
    {
        private readonly IClientService _clientService;
        private readonly INetworkService _networkService;
        private readonly IEventAggregator _aggregator;

        private static readonly SemaphoreSlim _updateLock = new(1, 1);
        private static readonly bool _disabled = Constants.DEBUG || ApiInfo.IsStoreRelease;

        private long _fileToken;

        private long? _chatId;
        private CloudUpdate _nextUpdate;

        private ulong _lastCheck;

        public CloudUpdateService(IClientService clientService, INetworkService networkService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _networkService = networkService;
            _aggregator = aggregator;
        }

        public CloudUpdate NextUpdate => _nextUpdate;

        public async void Update()
        {
            await UpdateAsync(false);
        }

        public static async Task<bool> LaunchAsync(INavigationService navigation, bool checkAvailability)
        {
            if (_disabled || !_updateLock.Wait(0))
            {
                Logger.Info("Can't acquire semaphore");
                return false;
            }

            try
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("updates", CreationCollisionOption.OpenIfExists);
                var files = await folder.GetFilesAsync();

                var current = Package.Current.Id.Version.ToVersion();
                var versions = new List<(Version Version, StorageFile File)>();

                Logger.Info($"Found {files.Count} files");

                foreach (var file in files)
                {
                    var split = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                    if (Version.TryParse(split, out Version version))
                    {
                        versions.Add((version, file));
                    }
                }

                Logger.Info($"Found {versions.Count} versions");

                var latest = versions.Max(x => x.Version);

                foreach (var update in versions)
                {
                    // If this isn't the most recent version and it isn't in use, just delete it
                    if (update.Version <= current || update.Version < latest)
                    {
                        Logger.Info($"{update.Version} is outdated, deleting");

                        await update.File.DeleteAsync();
                        continue;
                    }

                    Logger.Info($"Dispatching for version {update.Version}");

                    // Terminate notify icon to make the update process smoother
                    _ = NotifyIcon.ExitAsync();

                    // If package manager fails, we fall back on App Installer
                    await navigation.Dispatcher.DispatchAsync(async () =>
                    {
                        // Try to install the update first using the package manager
                        var installed = await InstallUpdateAsync(navigation, update.File);
                        if (installed is false)
                        {
                            // But only if App Installer is available
                            var result = checkAvailability
                                    ? await Launcher.QueryFileSupportAsync(update.File)
                                    : LaunchQuerySupportStatus.Available;

                            Logger.Info($"QueryFileSupportAsync: {result}");

                            if (result == LaunchQuerySupportStatus.Available)
                            {
                                await Launcher.LaunchFileAsync(update.File);
                                await BootStrapper.ConsolidateAsync();
                            }
                        }
                    });

                    _updateLock.Release();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            _updateLock.Release();
            return false;
        }

        private static async Task<bool> InstallUpdateAsync(INavigationService navigation, StorageFile file)
        {
            if (SettingsService.Current.Diagnostics.DisablePackageManager)
            {
                return false;
            }

            PackageManager pm = new();
            DeploymentResult result = null;
            CloudUpdatePopup popup = new();

            try
            {
                _ = popup.ShowQueuedAsync(navigation.XamlRoot);

                await Task.Run(async () =>
                {
                    var bundlePath = new Uri(file.Path);
                    var deployment = pm.AddPackageAsync(
                        bundlePath,
                        null,
                        DeploymentOptions.ForceApplicationShutdown);

                    deployment.Progress = (result, delta) =>
                    {
                        Logger.Info(string.Format("{0}, {1}%", delta.state, delta.percentage));
                        navigation.Dispatcher.Dispatch(() => popup.UpdateProgress(delta));
                    };

                    result = await deployment;
                });
            }
            catch (Exception ex)
            {
                if (result?.ExtendedErrorCode is not null)
                    Logger.Info(result.ErrorText);

                Logger.Error(ex);
                return false;
            }
            finally
            {
                popup.Destroy();
            }

            return result is { IsRegistered: true };
        }

        public async Task UpdateAsync(bool force)
        {
            if (_disabled || !_updateLock.Wait(0))
            {
                Logger.Info("Can't acquire lock");
                return;
            }

            var diff = Logger.TickCount - _lastCheck;
            var skip = diff < 5 * 60 * 1000;

            if (skip && !force)
            {
                _updateLock.Release();
                return;
            }

            _lastCheck = diff;

            var current = Package.Current.Id.Version.ToVersion();
            var cloud = await GetNextUpdateAsync();

            if (cloud != null && cloud.Version > current)
            {
                _nextUpdate = cloud;

                // There's a new version for the current font
                if (cloud.Document.Local.IsDownloadingCompleted || cloud.File != null)
                {
                    _aggregator.Publish(new UpdateAppVersion(cloud));
                }
                else if (cloud.Document.Local.CanBeDownloaded && !cloud.Document.Local.IsDownloadingActive)
                {
                    var date = Formatter.ToLocalTime(cloud.Date);
                    var epoch = DateTime.Now - date;

                    if (epoch.TotalDays >= 3 || !_networkService.IsMetered)
                    {
                        _clientService.DownloadFile(cloud.Document.Id, 16);
                        UpdateManager.Subscribe(cloud, _clientService, cloud.Document, ref _fileToken, UpdateFile, true);
                    }
                }
            }

            _updateLock.Release();
        }

        private async void UpdateFile(object target, File file)
        {
            var cloud = target as CloudUpdate;
            if (cloud == null)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("updates", CreationCollisionOption.OpenIfExists);
                var result = await TryCopyPartLocally(folder, file.Local.Path, cloud.Version);

                var current = Package.Current.Id.Version.ToVersion();
                if (current < cloud.Version)
                {
                    _nextUpdate = cloud;
                    _nextUpdate.File = result;

                    _aggregator.Publish(new UpdateAppVersion(cloud));
                }
            }
        }

        public async Task<CloudUpdate> GetNextUpdateAsync()
        {
            if (_disabled)
            {
                return null;
            }

            if (_chatId == null)
            {
                var chat = await _clientService.SendAsync(new SearchPublicChat(Constants.AppChannel)) as Chat;
                if (chat != null)
                {
                    _chatId = chat.Id;
                }
            }

            if (_chatId == null)
            {
                return null;
            }

            var updateChannel = SettingsService.Current.InstallBetaUpdates ? "#beta" : "#update";
            var chatId = _chatId.Value;

            await _clientService.SendAsync(new OpenChat(chatId));

            var messages = await _clientService.SendAsync(new SearchChatMessages(chatId, string.Empty, null, 0, 0, 10, new SearchMessagesFilterDocument(), 0, 0)) as FoundChatMessages;
            if (messages == null)
            {
                _clientService.Send(new CloseChat(chatId));
                return null;
            }

            _clientService.Send(new CloseChat(chatId));

            foreach (var message in messages.Messages)
            {
                var document = message.Content as MessageDocument;
                if (document == null)
                {
                    continue;
                }

                var hashtags = new List<string>();
                var changelog = string.Empty;

                foreach (var entity in document.Caption.Entities)
                {
                    if (entity.Type is TextEntityTypeHashtag)
                    {
                        hashtags.Add(document.Caption.Text.Substring(entity.Offset, entity.Length));
                    }
                    else if (entity.Type is TextEntityTypeCode)
                    {
                        changelog = document.Caption.Text.Substring(entity.Offset, entity.Length);
                    }
                }

                if (!hashtags.Contains(updateChannel) || !document.Document.FileName.Contains("x64") || !document.Document.FileName.EndsWith(".msixbundle"))
                {
                    continue;
                }

                var split = document.Document.FileName.Split('_');
                if (split.Length >= 3 && Version.TryParse(split[1], out Version version))
                {
                    var set = new CloudUpdate
                    {
                        MessageId = message.Id,
                        Changelog = changelog,
                        Version = version,
                        Document = document.Document.DocumentValue
                    };

                    var current = Package.Current.Id.Version.ToVersion();
                    var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("updates", CreationCollisionOption.OpenIfExists);

                    if (set.Version > current)
                    {
                        if (set.Document.Local.IsDownloadingCompleted)
                        {
                            set.File = await TryCopyPartLocally(folder, set.Document.Local.Path, set.Version);
                        }
                        else
                        {
                            set.File = await folder.TryGetItemAsync($"{set.Version}.msixbundle") as StorageFile;
                        }

                        return set;
                    }
                    else if (set.Document.Local.IsDownloadingCompleted)
                    {
                        // Delete the file from chat cache as it isn't needed anymore
                        _clientService.Send(new DeleteFileW(set.Document.Id));
                    }
                }
            }

            return null;
        }

        private static async Task<StorageFile> TryCopyPartLocally(StorageFolder folder, string path, Version version)
        {
            var fileName = $"{version}.msixbundle";

            var cache = await folder.TryGetItemAsync(fileName) as StorageFile;
            if (cache == null)
            {
                try
                {
                    var result = await StorageFile.GetFileFromPathAsync(path);
                    return await result.CopyAsync(folder, fileName, NameCollisionOption.FailIfExists);
                }
                catch { }
            }

            return cache;
        }
    }

    public partial class CloudUpdate
    {
        public long MessageId { get; set; }

        public Version Version { get; set; }
        public string Changelog { get; set; }

        public File Document { get; set; }
        public StorageFile File { get; set; }

        public int Date { get; set; }

        public bool UpdateFile(File file)
        {
            if (Document.Id == file.Id)
            {
                Document = file;
                return true;
            }

            return false;
        }
    }
}
