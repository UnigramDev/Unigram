using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsEmojiViewModel : TLViewModelBase, IDelegable<IFileDelegate>, IHandle<UpdateFile>
    {
        public SettingsEmojiViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new ItemsCollection(protoService);
        }

        public IFileDelegate Delegate { get; set; }

        public MvxObservableCollection<EmojiSet> Items { get; private set; }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Aggregator.Subscribe(this);
            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(pageState, suspending);
        }

        public void Handle(UpdateFile update)
        {
            BeginOnUIThread(() => Delegate?.UpdateFile(update.File));
        }

        class ItemsCollection : MvxObservableCollection<EmojiSet>, ISupportIncrementalLoading
        {
            private readonly IProtoService _protoService;
            private long? _chatId;
            private long _fromMessageId;

            private Dictionary<string, EmojiSet> _dict;

            private bool _hasMoreItems = true;

            public ItemsCollection(IProtoService protoService)
            {
                _protoService = protoService;
                _dict = new Dictionary<string, EmojiSet>();
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return AsyncInfo.Run(async (token) =>
                {
                    Add(new EmojiSet { Id = "apple", Title = "Apple", IsDefault = true, Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "apple.png") } } });
                    Add(new EmojiSet { Id = "microsoft", Title = "Microsoft", Document = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.ttf") } }, Thumbnail = new File { Local = new LocalFile { IsDownloadingCompleted = true, Path = System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Emoji", "microsoft.png") } } });

                    _hasMoreItems = false;
                    return new LoadMoreItemsResult { Count = 2 };

                    if (_chatId == null)
                    {
                        var responseChat = await _protoService.SendAsync(new SearchPublicChat("cGFnbGlhY2Npb19kaV9naGlhY2Npbw"));
                        if (responseChat is Chat chat)
                        {
                            _chatId = chat.Id;
                        }
                        else
                        {
                            _hasMoreItems = false;
                            return new LoadMoreItemsResult { Count = 0 };
                        }
                    }

                    var response = await _protoService.SendAsync(new SearchChatMessages(_chatId.Value, string.Empty, 0, _fromMessageId, 0, 100, new SearchMessagesFilterDocument()));
                    if (response is Messages messages)
                    {
                        _hasMoreItems = messages.MessagesValue.Count > 0;

                        foreach (var message in messages.MessagesValue)
                        {
                            _fromMessageId = message.Id;

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
                                if (_dict.TryGetValue(document.Document.FileName, out EmojiSet pack))
                                {
                                    _dict.Remove(document.Document.FileName);

                                    pack.Thumbnail = document.Document.DocumentValue;
                                    Add(pack);
                                }
                                else
                                {
                                    _dict[document.Document.FileName] = new EmojiSet
                                    {
                                        Thumbnail = document.Document.DocumentValue
                                    };
                                }
                            }
                            else
                            {
                                if (_dict.TryGetValue(document.Document.FileName, out EmojiSet pack))
                                {
                                    _dict.Remove(document.Document.FileName);

                                    pack.Id = document.Document.FileName;
                                    pack.Title = title;
                                    pack.Document = document.Document.DocumentValue;
                                    Add(pack);
                                }
                                else
                                {
                                    _dict[document.Document.FileName] = new EmojiSet
                                    {
                                        Id = document.Document.FileName,
                                        Title = title,
                                        Document = document.Document.DocumentValue
                                    };
                                }
                            }
                        }

                        return new LoadMoreItemsResult { Count = (uint)messages.MessagesValue.Count };
                    }
                    else
                    {
                        _hasMoreItems = false;
                        return new LoadMoreItemsResult { Count = 0 };
                    }
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }
    }

}
