//
// Copyright (c) Fela Ameghino 2015-2026
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
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.System;

namespace Telegram.Services
{
    public interface ICloudUpdateService
    {
        CloudUpdate NextUpdate { get; }
        Task UpdateAsync(bool force);
        Task<bool> LaunchAsync();
    }

    /// <summary>
    /// What the session registers for <see cref="ICloudUpdateService"/>: the resolver generator calls
    /// Create in place of a constructor, which is what lets the flavour decide the implementation.
    /// </summary>
    public static class CloudUpdateServiceFactory
    {
        public static ICloudUpdateService Create(IClientService clientService, INetworkService networkService, IEventAggregator aggregator)
        {
            // Nothing may install over a Store package but the Store, and the Store knows nothing about
            // the msixbundles the app channel carries, so neither implementation has anything to offer
            // the other's flavour.
            return ApiInfo.IsPackagedRelease
                ? new CloudUpdateService(clientService, networkService, aggregator)
                : new StoreUpdateService(aggregator);
        }
    }

    public partial class CloudUpdateService : ICloudUpdateService
    {
        private readonly IClientService _clientService;
        private readonly INetworkService _networkService;
        private readonly IEventAggregator _aggregator;

        private static readonly SemaphoreSlim _updateLock = new(1, 1);
#if UNPACKAGED
        // Without an identity there is no PackageManager to install with, so the update is a zip
        // that UnpackagedUpdates stages for Telegram.Updater - which only works in the folder
        // layout the installer creates.
        private static readonly bool _disabled = Constants.DEBUG || ApiInfo.IsStoreRelease || !(ApiInfo.HasPackageIdentity || (UnpackagedUpdates.IsInstalled && Constants.UnpackagedUpdatesChatId != 0));
#else
        private static readonly bool _disabled = Constants.DEBUG || ApiInfo.IsStoreRelease;
#endif

        private long _fileToken;

        // The update the token above is watching. The handler is told which file changed, not what
        // it was subscribed for, and this must stay the update the download was started for rather
        // than _nextUpdate, which a later check can have moved on.
        private CloudUpdate _fileCloud;

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

        public Task<bool> LaunchAsync()
        {
            return LaunchAsync(false);
        }

        public static async Task<bool> LaunchAsync(bool checkAvailability)
        {
            if (_disabled || !_updateLock.Wait(0))
            {
                Logger.Info("Can't acquire semaphore");
                return false;
            }

#if UNPACKAGED
            if (!ApiInfo.HasPackageIdentity)
            {
                try
                {
                    return await LaunchUnpackagedAsync();
                }
                finally
                {
                    _updateLock.Release();
                }
            }
#endif

            try
            {
                var folder = await AppPaths.LocalFolder.CreateFolderAsync("updates", CreationCollisionOption.OpenIfExists);
                var files = await folder.GetFilesAsync();

                var current = AppIdentity.Version.ToVersion();
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

                    var navigation = WindowContext.Main?.GetNavigationService();
                    if (navigation != null)
                    {
                        // Terminate notify icon to make the update process smoother
                        _ = SystemTray.HideAsync();

                        await navigation.Dispatcher.DispatchAsync(async () =>
                        {
                            // Try to install the update first using the package manager
                            // If package manager fails, we fall back on App Installer
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
                    }

                    _updateLock.Release();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }

            _updateLock.Release();
            return false;
        }

#if UNPACKAGED
        private static async Task<bool> LaunchUnpackagedAsync()
        {
            var navigation = WindowContext.Main?.GetNavigationService();
            if (navigation != null && UnpackagedUpdates.TryStartUpdater())
            {
                Logger.Info("Updater started");

                // The way Exit on the notification area icon ends the app, which the updater waits for.
                await navigation.Dispatcher.DispatchAsync(async () =>
                {
                    await SystemTray.HideAsync();
                    await BootStrapper.ConsolidateAsync();
                });

                return true;
            }

            return false;
        }
#endif

        private static async Task<bool> InstallUpdateAsync(INavigationService navigation, StorageFile file)
        {
            if (AppSettings.Diagnostics.DisablePackageManager)
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

                Logger.Exception(ex);
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
            if (_disabled)
            {
                return;
            }
            else if (!_updateLock.Wait(0))
            {
                Logger.Info("Can't acquire lock");
                return;
            }

            var diff = Logger.TickCount - _lastCheck;
            var skip = diff < 5 * 60 * 1000;

            if (skip && !force)
            {
                Logger.Info($"Checked {diff / 1000}s ago, skipping");
                _updateLock.Release();
                return;
            }

            _lastCheck = Logger.TickCount;

            var current = AppIdentity.Version.ToVersion();
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
                        Logger.Info($"Downloading {cloud.Version}");
                        _clientService.DownloadFile(cloud.Document.Id, 16);

                        _fileCloud = cloud;
                        UpdateManager.Subscribe(cloud, _clientService, cloud.Document, ref _fileToken, UpdateFile, true);
                    }
                }
            }

            _updateLock.Release();
        }

        private async void UpdateFile(File file)
        {
            var cloud = _fileCloud;
            if (cloud == null)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                var folder = await AppPaths.LocalFolder.CreateFolderAsync("updates", CreationCollisionOption.OpenIfExists);
                var result = await TryCopyPartLocally(folder, file.Local.Path, cloud.Version);
                Logger.Info(result != null ? $"Downloaded {cloud.Version}" : $"Downloaded {cloud.Version}, but it could not be prepared");

                var current = AppIdentity.Version.ToVersion();
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
                var chat = await _clientService.SendAsync(GetUpdatesChat()) as Chat;
                if (chat != null)
                {
                    _chatId = chat.Id;
                }
            }

            if (_chatId == null)
            {
                Logger.Info("Updates chat not found");
                return null;
            }

            var beta = AppSettings.InstallBetaUpdates;
            var chatId = _chatId.Value;

            await _clientService.SendAsync(new OpenChat(chatId));

            var messages = await _clientService.SendAsync(new SearchChatMessages(chatId, null, string.Empty, null, 0, 0, 10, new SearchMessagesFilterDocument())) as FoundChatMessages;
            if (messages == null)
            {
                Logger.Info("Updates chat search failed");
                _clientService.Send(new CloseChat(chatId));
                return null;
            }

            _clientService.Send(new CloseChat(chatId));

            var current = AppIdentity.Version.ToVersion();
            CloudUpdate next = null;

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

                if (!hashtags.Contains("#update") && !(beta && hashtags.Contains("#beta")))
                {
                    continue;
                }

                if (!TryParseFileName(document.Document.FileName, out Version version))
                {
                    continue;
                }

                // The highest version rather than the latest post: the two channels are posted
                // independently, so a stable release can come after a beta that is still ahead of it.
                if (version > current && (next == null || version > next.Version))
                {
                    next = new CloudUpdate
                    {
                        MessageId = message.Id,
                        Changelog = changelog,
                        Version = version,
                        Document = document.Document.DocumentValue
                    };
                }
                else if (document.Document.DocumentValue.Local.IsDownloadingCompleted)
                {
                    // Delete the file from chat cache as it isn't needed anymore
                    _clientService.Send(new DeleteFile(document.Document.DocumentValue.Id));
                }
            }

            if (next == null)
            {
                Logger.Info($"No update among {messages.Messages.Count} posts, running {current}");
                return null;
            }

            Logger.Info($"Found {next.Version}, running {current}");

            var folder = await AppPaths.LocalFolder.CreateFolderAsync("updates", CreationCollisionOption.OpenIfExists);

            if (next.Document.Local.IsDownloadingCompleted)
            {
                next.File = await TryCopyPartLocally(folder, next.Document.Local.Path, next.Version);
            }
            else
            {
                next.File = await PrepareAsync(await folder.TryGetItemAsync($"{next.Version}{Extension}") as StorageFile, next.Version);
            }

            return next;
        }

        private static Function GetUpdatesChat()
        {
#if UNPACKAGED
            // A private channel can only be found, never resolved: TDLib knows it, and holds the
            // access hash every request to it needs, only when this account is a member. So
            // membership is what decides who gets these updates.
            if (!ApiInfo.HasPackageIdentity)
            {
                return new GetChat(Constants.UnpackagedUpdatesChatId);
            }
#endif

            return new SearchPublicChat(Constants.AppChannel);
        }

        private static bool TryParseFileName(string fileName, out Version version)
        {
#if UNPACKAGED
            if (!ApiInfo.HasPackageIdentity)
            {
                return UnpackagedUpdates.TryParseFileName(fileName, out version);
            }
#endif

            version = null;

            if (!fileName.Contains("x64") || !fileName.EndsWith(".msixbundle"))
            {
                return false;
            }

            var split = fileName.Split('_');
            return split.Length >= 3 && Version.TryParse(split[1], out version);
        }

#if UNPACKAGED
        private static string Extension => ApiInfo.HasPackageIdentity ? ".msixbundle" : UnpackagedUpdates.Extension;
#else
        private const string Extension = ".msixbundle";
#endif

        // Unpackaged, a download is not ready to restart into until it has been extracted.
        private static Task<StorageFile> PrepareAsync(StorageFile file, Version version)
        {
#if UNPACKAGED
            if (!ApiInfo.HasPackageIdentity)
            {
                return UnpackagedUpdates.StageAsync(file, version);
            }
#endif

            return Task.FromResult(file);
        }

        private static async Task<StorageFile> TryCopyPartLocally(StorageFolder folder, string path, Version version)
        {
            var fileName = $"{version}{Extension}";

            var cache = await folder.TryGetItemAsync(fileName) as StorageFile;
            if (cache == null)
            {
                try
                {
                    var result = await StorageFile.GetFileFromPathAsync(path);
                    cache = await result.CopyAsync(folder, fileName, NameCollisionOption.FailIfExists);
                }
                catch { }
            }

            return await PrepareAsync(cache, version);
        }
    }

    public partial class CloudUpdate
    {
        public long MessageId { get; set; }

        public Version Version { get; set; }
        public string Changelog { get; set; }

        public File Document { get; set; }
        public StorageFile File { get; set; }

        // A Store update has neither: there is no bundle to fetch from the app channel, and Document is
        // null, so nothing may reach through it without checking this first.
        public bool IsStorePackage { get; set; }

        public bool IsReady => File != null || IsStorePackage;

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
