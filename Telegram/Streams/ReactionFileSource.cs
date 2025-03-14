//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public partial class ReactionFileSource : DelayedFileSource
    {
        private readonly IClientService _clientService;
        private readonly ReactionType _reaction;

        public ReactionFileSource(IClientService clientService, ReactionType reaction)
            : base(clientService, null as File)
        {
            _clientService = clientService;
            _reaction = reaction;

            DownloadFile(null, null);
        }

        public bool UseCenterAnimation { get; set; }

        public override long Id => GetHashCode();

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
                    Sticker sticker = null;
                    if (_reaction is ReactionTypeEmoji emoji)
                    {
                        var response = await _clientService.SendAsync(new GetEmojiReaction(emoji.Emoji));
                        if (response is EmojiReaction reaction)
                        {
                            sticker = UseCenterAnimation
                                ? reaction.CenterAnimation
                                : reaction.ActivateAnimation;

                            if (UseCenterAnimation && reaction.AroundAnimation != null)
                            {
                                _clientService.DownloadFile(reaction.AroundAnimation.StickerValue.Id, 8);
                            }
                        }
                    }
                    else if (_reaction is ReactionTypeCustomEmoji customEmoji)
                    {
                        var response = await _clientService.SendAsync(new GetCustomEmojiStickers(new[] { customEmoji.CustomEmojiId }));
                        if (response is Stickers stickers && stickers.StickersValue.Count == 1)
                        {
                            sticker = stickers.StickersValue[0];
                        }
                    }
                    else if (_reaction is ReactionTypePaid)
                    {
                        sticker = new Sticker(0, 0, 512, 512, "\u2B50", new StickerFormatTgs(), new StickerFullTypeRegular(), null, TdExtensions.GetLocalFile("Assets\\Animations\\PaidReactionCenter.tgs"));
                    }

                    if (sticker != null)
                    {
                        _file = sticker.StickerValue;
                        Format = sticker.Format;
                        Width = sticker.Width;
                        Height = sticker.Height;
                        NeedsRepainting = sticker.FullType is StickerFullTypeCustomEmoji { NeedsRepainting: true };

                        OnOutlineChanged(sticker.StickerValue);
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

            return _reaction switch
            {
                ReactionTypeEmoji emoji => emoji.Emoji.GetHashCode(),
                ReactionTypeCustomEmoji customEmoji => customEmoji.CustomEmojiId.GetHashCode(),
                _ => base.GetHashCode()
            };
        }
    }
}
