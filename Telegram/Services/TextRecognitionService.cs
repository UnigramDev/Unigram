//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native.AI;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;

namespace Telegram.Services
{
    public interface ITextRecognitionService
    {
        Task<TextRecognitionStatus> EnsureReadyAsync();
    }

    public abstract class TextRecognitionStatus
    {

    }

    public class TextRecognitionStatusAvailable : TextRecognitionStatus
    {
        public TextRecognitionStatusAvailable(ITextRecognizer recognizer)
        {
            Recognizer = recognizer;
        }

        public ITextRecognizer Recognizer { get; }
    }

    public class TextRecognitionStatusDownloading : TextRecognitionStatus
    {
        public TextRecognitionStatusDownloading(File document)
        {
            Document = document;
        }

        public File Document { get; }
    }

    public class TextRecognitionStatusUnavailable : TextRecognitionStatus
    {

    }

    public partial class TextRecognitionService : ITextRecognitionService
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private static readonly SemaphoreSlim _extractLock = new(1, 1);

        private long _fileToken;

        private long? _chatId;

        public TextRecognitionService(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;
        }

        private async void UpdateFile(object target, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                var storage = await _clientService.GetFileAsync(file);
                if (storage == null)
                {
                    return;
                }

                var destination = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Ocr", CreationCollisionOption.OpenIfExists);
                await ExtractModelAsync(destination, storage, file);
            }
        }

        public async Task<TextRecognitionStatus> EnsureReadyAsync()
        {
            var destination = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Ocr", CreationCollisionOption.OpenIfExists);

            var available = await destination.TryGetItemAsync("oneocr.onemodel");
            if (available != null)
            {
                return TryCreateRecognizer();
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
                return new TextRecognitionStatusUnavailable();
            }

            var chatId = _chatId.Value;
            var fileName = string.Format("Ocr_{0}.zip", Package.Current.Id.Architecture switch
            {
                ProcessorArchitecture.X64 => "x64",
                ProcessorArchitecture.Arm64 => "arm64",
                _ => "unsupported"
            });

            await _clientService.SendAsync(new OpenChat(chatId));

            var messages = await _clientService.SendAsync(new SearchChatMessages(chatId, null, fileName, null, 0, 0, 10, new SearchMessagesFilterDocument())) as FoundChatMessages;
            if (messages == null)
            {
                _clientService.Send(new CloseChat(chatId));
                return new TextRecognitionStatusUnavailable();
            }

            _clientService.Send(new CloseChat(chatId));

            foreach (var message in messages.Messages)
            {
                var document = message.Content as MessageDocument;
                if (document == null)
                {
                    continue;
                }

                if (document.Document.FileName != fileName)
                {
                    continue;
                }

                var storage = await _clientService.GetFileAsync(document.Document.DocumentValue);
                if (storage != null)
                {
                    await ExtractModelAsync(destination, storage, document.Document.DocumentValue);
                    return TryCreateRecognizer();
                }
                else
                {
                    _clientService.DownloadFile(document.Document.DocumentValue.Id, 16);
                    UpdateManager.Subscribe(this, _clientService, document.Document.DocumentValue, ref _fileToken, UpdateFile, true);

                    return new TextRecognitionStatusDownloading(document.Document.DocumentValue);
                }
            }

            return new TextRecognitionStatusUnavailable();
        }

        private TextRecognitionStatus TryCreateRecognizer()
        {
            var recognizer = TextRecognizer.GetOne(Constants.TextRecognizerModelKey);
            if (recognizer != null)
            {
                return new TextRecognitionStatusAvailable(recognizer);
            }

            return new TextRecognitionStatusUnavailable();
        }

        private async Task ExtractModelAsync(StorageFolder destination, StorageFile file, File document)
        {
            if (!_extractLock.Wait(0))
            {
                // Already in progress
                return;
            }

            using (var zipStream = await file.OpenReadAsync())
            using (var zipInput = System.IO.WindowsRuntimeStreamExtensions.AsStreamForRead(zipStream))
            using (var archive = new System.IO.Compression.ZipArchive(zipInput))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    var entryPath = entry.FullName.Replace('/', '\\');
                    var entryFile = await destination.CreateFileAsync(entryPath, CreationCollisionOption.ReplaceExisting);

                    using (var entryStream = entry.Open())
                    using (var outStream = await System.IO.WindowsRuntimeStorageExtensions.OpenStreamForWriteAsync(entryFile))
                    {
                        await entryStream.CopyToAsync(outStream);
                    }
                }
            }

            _clientService.Send(new DeleteFile(document.Id));
            _aggregator.Publish(new UpdateTextRecognition());

            _extractLock.Release();
        }
    }
}
