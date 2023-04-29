//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Telegram.Services
{
    public interface ICloudUpdateService
    {
        CloudUpdate NextUpdate { get; }

        Task UpdateAsync(bool force);
        //Task<CloudUpdate> GetNextUpdateAsync();
        //Task<IList<CloudUpdate>> GetHistoryAsync();
    }

    public class CloudUpdateService : ICloudUpdateService
    {
        private readonly FileContext<CloudUpdate> _mapping = new FileContext<CloudUpdate>();

        private readonly IClientService _clientService;
        private readonly INetworkService _networkService;
        private readonly IEventAggregator _aggregator;

        private long? _chatId;
        private CloudUpdate _nextUpdate;

        private long _lastCheck;
        private bool _checking;

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

        public async Task UpdateAsync(bool force)
        {
            if (ApiInfo.IsStoreRelease)
            {
                return;
            }

            var diff = Environment.TickCount - _lastCheck;
            var skip = diff < 5 * 60 * 1000 || _checking;

            if (skip && !force)
            {
                return;
            }

            _checking = true;
            _lastCheck = diff;

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("updates", CreationCollisionOption.OpenIfExists);
            var files = await folder.GetFilesAsync();

            var current = Package.Current.Id.Version.ToVersion();
            var sets = new List<Version>();

            foreach (var file in files)
            {
                var split = System.IO.Path.GetFileNameWithoutExtension(file.Name);
                if (Version.TryParse(split, out Version version))
                {
                    sets.Add(version);
                }
            }

            foreach (var version in sets)
            {
                // If this isn't the most recent version and it isn't in use, just delete it
                if (version <= current)
                {
                    var file = await folder.TryGetItemAsync($"{version}.msixbundle") as StorageFile;
                    if (file != null)
                    {
                        await file.DeleteAsync();
                    }
                }

                // We can safely ignore any other version as next steps will take care of them
            }

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
                        UpdateManager.Subscribe(cloud, _clientService, cloud.Document, UpdateFile, true);
                    }
                }
            }

            // This call is needed to delete old updates from disk
            await GetHistoryAsync();

            _checking = false;
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
            if (ApiInfo.IsStoreRelease)
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

            var messages = await _clientService.SendAsync(new SearchChatMessages(chatId, string.Empty, null, 0, 0, 10, new SearchMessagesFilterDocument(), 0)) as FoundChatMessages;
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

        public async Task<IList<CloudUpdate>> GetHistoryAsync()
        {
            if (ApiInfo.IsStoreRelease)
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

            var response = await _clientService.SendAsync(new SearchChatMessages(chatId, updateChannel, null, 0, 0, 10, new SearchMessagesFilterDocument(), 0)) as FoundChatMessages;
            if (response == null)
            {
                _clientService.Send(new CloseChat(chatId));
                return null;
            }

            _clientService.Send(new CloseChat(chatId));

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("updates", CreationCollisionOption.OpenIfExists);

            var dict = new List<CloudUpdate>();
            var thumbnails = new Dictionary<string, File>();

            var results = new List<CloudUpdate>();

            foreach (var message in response.Messages)
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
                        Document = document.Document.DocumentValue,
                        Date = message.Date
                    };

                    dict.Add(set);
                }
            }

            var current = Package.Current.Id.Version.ToVersion();
            var latest = dict.Count > 0 ? dict.Max(x => x.Version) : null;

            foreach (var set in dict)
            {
                if (set.Version < current && set.Document.Local.IsDownloadingCompleted)
                {
                    // Delete the file from chat cache as it isn't needed anymore
                    _clientService.Send(new DeleteFileW(set.Document.Id));
                }

                results.Add(set);
            }

            return results.OrderByDescending(x => x.Version).ToList();
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

    public class CloudUpdate
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
