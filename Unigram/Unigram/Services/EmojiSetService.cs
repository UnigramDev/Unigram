using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services.Settings;
using Windows.Storage;

namespace Unigram.Services
{
    public interface IEmojiSetService
    {
        Task UpdateAsync();
        Task<IList<EmojiSet>> GetCloudSetsAsync();
    }

    public class EmojiSetService : IEmojiSetService, IHandle<UpdateFile>
    {
        private readonly FileContext<EmojiSet> _mapping = new FileContext<EmojiSet>();

        private readonly IProtoService _protoService;
        private readonly ISettingsService _settings;
        private readonly IEventAggregator _aggregator;

        private long? _chatId;

        public EmojiSetService(IProtoService protoService, ISettingsService settings, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _settings = settings;
            _aggregator = aggregator;

            _aggregator.Subscribe(this);
        }

        public async Task UpdateAsync()
        {
            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("emoji", CreationCollisionOption.OpenIfExists);
            var files = await folder.GetFilesAsync();

            var current = _settings.Appearance.EmojiSet;
            var sets = new Dictionary<string, List<int>>();

            foreach (var file in files)
            {
                var split = file.Name.Split('.');
                if (split.Length == 3 && split[2] == "ttf" && int.TryParse(split[1], out int version))
                {
                    if (sets.TryGetValue(split[0], out var versions))
                    {
                        versions.Add(version);
                    }
                    else
                    {
                        sets[split[0]] = new List<int> { version };
                    }
                }
            }

            foreach (var set in sets)
            {
                var latest = set.Value.Max();

                foreach (var version in set.Value)
                {
                    // If this is the sticker set in use but we have a newer version set it for the next app launch
                    if (set.Key == current.Id && version == current.Version && version < latest)
                    {
                        _settings.Appearance.EmojiSet = current = new InstalledEmojiSet { Id = current.Id, Title = current.Title, Version = latest };
                    }
                    // If this isn't the most recent version and it isn't in use, just delete it
                    else if (version < latest)
                    {
                        var file = await folder.TryGetItemAsync($"{set.Key}.{version}.ttf") as StorageFile;
                        if (file != null)
                        {
                            try
                            {
                                await file.DeleteAsync();
                            }
                            catch { }
                        }
                    }
                }
            }

            var online = await GetCloudSetsAsync();

            foreach (var item in online)
            {
                if (sets.TryGetValue(item.Id, out var installed) && item.Version > installed.Max())
                {
                    // There's a new version for the current font
                    if (item.Document.Local.CanBeDownloaded && !item.Document.Local.IsDownloadingActive && !item.Document.Local.IsDownloadingCompleted)
                    {
                        _protoService.DownloadFile(item.Document.Id, 16);
                    }

                    if (item.Thumbnail.Local.CanBeDownloaded && !item.Thumbnail.Local.IsDownloadingActive && !item.Thumbnail.Local.IsDownloadingCompleted)
                    {
                        _protoService.DownloadFile(item.Thumbnail.Id, 16);
                    }
                }
            }
        }

        public async void Handle(UpdateFile update)
        {
            var file = update.File;
            if (_mapping.TryGetValue(file.Id, out List<EmojiSet> sets))
            {
                foreach (var set in sets)
                {
                    set.UpdateFile(file);
                }

                var emojiSet = sets.FirstOrDefault();
                if (emojiSet == null)
                {
                    return;
                }

                if (file.Local.IsDownloadingCompleted)
                {
                    var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("emoji", CreationCollisionOption.OpenIfExists);
                    await TryCopyPartLocally(folder, file.Local.Path, emojiSet.Id, emojiSet.Version, file.Id == emojiSet.Document.Id);

                    var current = _settings.Appearance.EmojiSet;

                    if (file.Id == emojiSet.Document.Id && current.Id == emojiSet.Id && current.Version <= emojiSet.Version)
                    {
                        await UpdateAsync();
                    }
                }
            }
        }

        public async Task<IList<EmojiSet>> GetCloudSetsAsync()
        {
            if (_chatId == null)
            {
                var chat = await _protoService.SendAsync(new SearchPublicChat(Constants.AppChannel)) as Chat;
                if (chat != null)
                {
                    _chatId = chat.Id;
                }
            }

            if (_chatId == null)
            {
                return new EmojiSet[0];
            }

            var chatId = _chatId.Value;
            await _protoService.SendAsync(new OpenChat(chatId));

            var response = await _protoService.SendAsync(new SearchChatMessages(chatId, "#emoji", 0, 0, 0, 100, null, 0)) as Messages;
            if (response == null)
            {
                _protoService.Send(new CloseChat(chatId));
                return new EmojiSet[0];
            }

            _protoService.Send(new CloseChat(chatId));

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("emoji", CreationCollisionOption.OpenIfExists);

            var dict = new Dictionary<string, List<EmojiSet>>();
            var thumbnails = new Dictionary<string, File>();

            var results = new List<EmojiSet>();

            foreach (var message in response.MessagesValue)
            {
                var document = message.Content as MessageDocument;
                if (document == null)
                {
                    continue;
                }

                var hashtags = new List<string>();
                var title = string.Empty;

                foreach (var entity in document.Caption.Entities)
                {
                    if (entity.Type is TextEntityTypeHashtag)
                    {
                        hashtags.Add(document.Caption.Text.Substring(entity.Offset, entity.Length));
                    }
                    else if (entity.Type is TextEntityTypeCode)
                    {
                        title = document.Caption.Text.Substring(entity.Offset, entity.Length);
                    }
                }

                if (!hashtags.Contains("#emoji"))
                {
                    continue;
                }

                if (hashtags.Contains("#preview"))
                {
                    if (dict.TryGetValue(document.Document.FileName, out List<EmojiSet> sets))
                    {
                        foreach (var set in sets)
                        {
                            _mapping[document.Document.DocumentValue.Id].Add(set);
                            set.Thumbnail = document.Document.DocumentValue;
                        }
                    }
                    else
                    {
                        thumbnails[document.Document.FileName] = document.Document.DocumentValue;
                    }
                }
                else
                {
                    var versionKey = hashtags.FirstOrDefault(x => x.StartsWith("#v"));
                    var positionKey = hashtags.FirstOrDefault(x => x.StartsWith("#p"));

                    var version = 1;
                    var position = 1;

                    if (int.TryParse(versionKey.Substring(2), out int v))
                    {
                        version = v;
                    }
                    if (int.TryParse(positionKey.Substring(2), out int p))
                    {
                        position = p;
                    }

                    var set = new EmojiSet
                    {
                        Id = document.Document.FileName,
                        Title = title,
                        Version = version,
                        Document = document.Document.DocumentValue
                    };

                    _mapping[document.Document.DocumentValue.Id].Add(set);

                    if (thumbnails.TryGetValue(document.Document.FileName, out File thumbnail))
                    {
                        set.Thumbnail = thumbnail;
                    }

                    if (dict.TryGetValue(document.Document.FileName, out List<EmojiSet> sets))
                    {
                        sets.Add(set);
                    }
                    else
                    {
                        dict[document.Document.FileName] = new List<EmojiSet> { set };
                    }
                }
            }

            foreach (var sets in dict.Values)
            {
                var latest = sets.Max(x => x.Version);

                foreach (var set in sets)
                {
                    if (set.Version == latest)
                    {
                        if (set.Thumbnail.Local.IsDownloadingCompleted)
                        {
                            await TryCopyPartLocally(folder, set.Thumbnail.Local.Path, set.Id, 0, false);
                        }

                        if (set.Document.Local.IsDownloadingCompleted)
                        {
                            await TryCopyPartLocally(folder, set.Document.Local.Path, set.Id, set.Version, true);
                        }

                        results.Add(set);
                    }
                    else
                    {
                        // Delete the file from chat cache as it isn't needed anymore
                        _protoService.Send(new DeleteFileW(set.Document.Id));
                    }
                }
            }

            return results;
        }

        public static async Task TryCopyPartLocally(StorageFolder folder, string path, string id, int version, bool font)
        {
            var fileName = font ? $"{id}.{version}.ttf" : $"{id}.png";

            var cache = await folder.TryGetItemAsync(fileName);
            if (cache == null)
            {
                try
                {
                    var result = await StorageFile.GetFileFromPathAsync(path);
                    await result.CopyAsync(folder, fileName, NameCollisionOption.FailIfExists);
                }
                catch { }
            }
        }
    }
}
