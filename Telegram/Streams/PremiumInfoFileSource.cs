using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public partial class PremiumInfoFileSource : DelayedFileSource
    {
        private readonly IClientService _clientService;
        private readonly int _monthCount;

        public PremiumInfoFileSource(IClientService clientService, int monthCount)
            : base(clientService, null as File)
        {
            _clientService = clientService;
            _monthCount = monthCount;

            DownloadFile(null, null);
        }

        public override long Id => _monthCount.GetHashCode();

        public override async void DownloadFile(object sender, UpdateHandler<File> handler)
        {
            if (_file != null && _file.Local.IsDownloadingCompleted)
            {
                handler?.Invoke(sender, _file);
            }
            else
            {
                if (_file == null)
                {
                    var response = await _clientService.SendAsync(new GetPremiumInfoSticker(_monthCount));
                    if (response is Sticker sticker)
                    {
                        _file = sticker.StickerValue;
                        Format = sticker.Format;
                        Width = sticker.Width;
                        Height = sticker.Height;
                        Outline = sticker.Outline;
                        NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };

                        OnOutlineChanged();
                    }
                }

                if (_file == null)
                {
                    return;
                }
                else if (_file.Local.IsDownloadingCompleted)
                {
                    handler?.Invoke(sender, _file);
                    return;
                }

                if (handler != null)
                {
                    UpdateManager.Subscribe(sender, _clientService, _file, ref _fileToken, handler, true);
                }

                if (_file.Local.CanBeDownloaded /*&& !_file.Local.IsDownloadingActive*/)
                {
                    _clientService.DownloadFile(_file.Id, 16);
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is CustomEmojiFileSource y && !y.IsUnique && !IsUnique)
            {
                return y.Id == Id;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (IsUnique)
            {
                return base.GetHashCode();
            }

            return _monthCount.GetHashCode();
        }
    }
}
