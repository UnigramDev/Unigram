//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Streams
{
    public partial class ReactionFileSource : DelayedFileSource
    {
        private readonly ReactionType _reaction;

        public ReactionFileSource(IClientService clientService, ReactionType reaction)
            : base(clientService, null as File)
        {
            _reaction = reaction;

            DownloadFile(null, DelayedFileDownload.Loaded, null);
        }

        private ReactionFileSource(IClientService clientService, ReactionType reaction, File file)
            : base(clientService, file)
        {
            _reaction = reaction;

            if (file == null)
            {
                DownloadFile(null, DelayedFileDownload.Loaded, null);
            }
        }

        public ReactionFileSource Clone(bool animated)
        {
            return new ReactionFileSource(_clientService, _reaction, _file)
            {
                IsUnique = !animated,
                IsAnimated = animated,
                UseCenterAnimation = true,
                Format = Format,
                Width = Width,
                Height = Height,
                NeedsRepainting = NeedsRepainting
            };
        }

        public bool UseCenterAnimation { get; set; }

        public override long Id => GetHashCode();

        public override async void DownloadFile(object sender, DelayedFileDownload download, UpdateHandler<File> handler)
        {
            if (_file != null && _file.Local.IsDownloadingCompleted && download != DelayedFileDownload.Unloaded)
            {
                handler?.Invoke(sender, _file);
            }
            else
            {
                if (_file == null && download != DelayedFileDownload.Unloaded)
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
                    }
                }

                if (_file == null)
                {
                    return;
                }
                else if (_file.Local.IsDownloadingCompleted && download != DelayedFileDownload.Unloaded)
                {
                    handler?.Invoke(sender, _file);
                    return;
                }

                if (handler != null && download != DelayedFileDownload.Unloaded)
                {
                    UpdateManager.Subscribe(sender, _clientService, _file, ref _fileToken, handler, true);
                }

                if (_file.Local.CanBeDownloaded /*&& !_file.Local.IsDownloadingActive*/)
                {
                    _clientService.DownloadFile(_file.Id, download == DelayedFileDownload.Playing ? 16 : 15);
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is CustomEmojiFileSource y && !y.IsUnique && !IsUnique)
            {
                return y.Id == Id && y.IsAnimated == IsAnimated;
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
                ReactionTypeEmoji emoji => HashCode.Combine(emoji.Emoji, IsAnimated),
                ReactionTypeCustomEmoji customEmoji => HashCode.Combine(customEmoji.CustomEmojiId, IsAnimated),
                ReactionTypePaid paid => HashCode.Combine("\u2B50", IsAnimated),
                _ => base.GetHashCode()
            };
        }
    }
}
