//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace Telegram.ViewModels
{
    public abstract class DialogPendingMessage
    {
        protected readonly MessageViewModel _message;
        protected readonly DispatcherTimer _timer;
        protected DispatcherTimer _typing;

        protected readonly Random _random = new();

        protected int _pendingLength;
        protected int _textLength;

        protected Message _completed;

        public DialogPendingMessage(UpdatePendingMessage update, MessageViewModel message)
        {
            _message = message;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(message.ClientService.Options.PendingTextMessagePeriod)
            };

            _timer.Tick += OnTick;
            _timer.Start();

            DraftId = update.DraftId;
            LastUpdate = Logger.TickCount;
        }

        private int GetRandomChunkSize(int remainingLength)
        {
            if (remainingLength <= 10)
            {
                return remainingLength;
            }

            float speedMultiplier = GetSpeedMultiplier(_pendingLength);

            var rand = _random.NextDouble();
            int baseSize;

            if (rand < 0.6)
                baseSize = 2 + (int)Math.Floor(_random.NextDouble() * 4);
            else if (rand < 0.9)
                baseSize = 6 + (int)Math.Floor(_random.NextDouble() * 3);
            else
                baseSize = 9 + (int)Math.Floor(_random.NextDouble() * 2);

            int adjustedSize = (int)Math.Ceiling(baseSize * speedMultiplier);

            return Math.Min(Math.Min(adjustedSize, 20), remainingLength);
        }

        private TimeSpan GetRandomDelay(char lastChar)
        {
            float speedMultiplier = GetSpeedMultiplier(_pendingLength);

            double baseDelay;
            if (lastChar is '.' or '!' or '?')
            {
                baseDelay = 50 + _random.NextDouble() * 30;
            }
            else if (lastChar == ',')
            {
                baseDelay = 30 + _random.NextDouble() * 20;
            }
            else
            {
                baseDelay = 15 + _random.NextDouble() * 20;
            }

            double adjustedDelay = baseDelay / speedMultiplier;
            adjustedDelay = Math.Max(adjustedDelay, 8);

            return TimeSpan.FromMilliseconds(adjustedDelay);
        }

        private float GetSpeedMultiplier(int remainingLength)
        {
            if (remainingLength < 200) return 1.0f;
            if (remainingLength < 500) return 1.3f;
            if (remainingLength < 1000) return 1.6f;
            if (remainingLength < 2000) return 2.0f;
            return 2.5f;
        }

        public long DraftId { get; }

        private void OnTick(object sender, object e)
        {
            _timer.Stop();
            Completed?.Invoke(this, null);
        }

        public ulong LastUpdate { get; protected set; }

        public void Update(UpdatePendingMessage update)
        {
            _timer.Stop();
            _timer.Start();

            LastUpdate = Logger.TickCount;
            OnUpdate(update);

            //if (update.Content is MessageText messageText)
            //{
            //    Update(messageText.Text);
            //}
        }

        protected abstract void OnUpdate(UpdatePendingMessage update);

        public void Update(Message message)
        {
            _timer.Stop();
            _completed = message;

            LastUpdate = Logger.TickCount;
            //Update(message.GetCaption());
            OnUpdate(message);
        }

        protected abstract void OnUpdate(Message message);

        protected void Typing_Tick(object sender, object e)
        {
            if (_typing == null)
            {
                _typing = new DispatcherTimer();
                _typing.Tick += Typing_Tick;
            }
            else
            {
                _typing.Stop();
            }

            var length = GetRandomChunkSize(_pendingLength - _textLength);

            //_text = _pending.Substring(0, _text.Text.Length + length);
            OnTyping(length);

            RaiseUpdate();
        }

        protected abstract void OnTyping(int length);

        protected void RaiseUpdate()
        {
            if (_completed != null && _textLength == _pendingLength)
            {
                _timer.Stop();
                Completed?.Invoke(this, _completed);
            }
            else
            {
                _message.Content = CreateContent();
                Updated?.Invoke(this, _message);
            }

            if (_textLength < _pendingLength)
            {
                _typing.Interval = GetRandomDelay(/*_text.Text.Length > 0 ? _text.Text[^1] :*/ 'a');
                _typing.Start();
            }
        }

        protected abstract MessageContent CreateContent();

        public void Stop()
        {
            _timer.Stop();
            _typing.Stop();
        }

        public event TypedEventHandler<DialogPendingMessage, MessageViewModel> Updated;

        public event TypedEventHandler<DialogPendingMessage, Message> Completed;

        protected void RaiseCompleted()
        {
            Completed?.Invoke(this, _completed);
        }
    }

    public class DialogPendingTextMessage2 : DialogPendingMessage
    {
        private FormattedText _text;
        private FormattedText _pending;

        public DialogPendingTextMessage2(UpdatePendingMessage update, MessageViewModel message)
            : base(update, message)
        {
            _text = string.Empty.AsFormattedText();
            _textLength = 0;

            if (update.Content is MessageText messageText)
            {
                _pending = messageText.Text;
                _pendingLength = messageText.Text.Text.Length;
            }

            Typing_Tick(null, null);
        }

        protected override void OnUpdate(UpdatePendingMessage update)
        {
            if (update.Content is MessageText messageText)
            {
                Update(messageText.Text);
            }
        }

        protected override void OnUpdate(Message message)
        {
            Update(message.GetCaption());
        }

        private void Update(FormattedText text)
        {
            if (text == null)
            {
                _timer.Stop();
                _typing.Stop();
                RaiseCompleted();

                return;
            }

            if (text.Text.StartsWith(_text.Text))
            {
                _pending = text;
                _pendingLength = text.Text.Length;
            }
            else if (text.Text.Length > _text.Text.Length)
            {
                _text = text.Substring(0, _text.Text.Length);
                _textLength = _text.Text.Length;

                _pending = text;
                _pendingLength = text.Text.Length;
            }
            else
            {
                _text = text;
                _textLength = text.Text.Length;

                _pending = text;
                _pendingLength = text.Text.Length;
            }

            if (_typing.IsEnabled)
            {
                return;
            }

            RaiseUpdate();
        }

        protected override MessageContent CreateContent()
        {
            return new MessageText(_text, null, null);
        }

        protected override void OnTyping(int length)
        {
            _text = _pending.Substring(0, _text.Text.Length + length);
            _textLength = _text.Text.Length;
        }
    }

    public class DialogPendingRichMessage : DialogPendingMessage
    {
        private IList<PageBlock> _text;
        private IList<PageBlock> _pending;

        public DialogPendingRichMessage(UpdatePendingMessage update, MessageViewModel message)
            : base(update, message)
        {
            _text = Array.Empty<PageBlock>();
            _textLength = 0;

            if (update.Content is MessageRichMessage messageRich)
            {
                _pending = messageRich.Message.Blocks;
                _pendingLength = PageBlockStreaming.Length(messageRich.Message.Blocks);
            }

            Typing_Tick(null, null);
        }

        protected override void OnUpdate(UpdatePendingMessage update)
        {
            if (update.Content is MessageRichMessage messageRich)
            {
                Update(messageRich.Message.Blocks);
            }
        }

        protected override void OnUpdate(Message message)
        {
            if (message.Content is MessageRichMessage messageRich)
            {
                Update(messageRich.Message.Blocks);
            }
        }

        private void Update(IList<PageBlock> blocks)
        {
            //if (text == null)
            //{
            //    _timer.Stop();
            //    _typing.Stop();
            //    Completed?.Invoke(this, _completed);

            //    return;
            //}

            var textLength = PageBlockStreaming.Length(blocks);
            if (textLength > _textLength)
            {
                _text = PageBlockStreaming.Substring(blocks, _textLength);
                _textLength = PageBlockStreaming.Length(_text);

                _pending = blocks;
                _pendingLength = textLength;
            }
            else
            {
                _text = blocks;
                _textLength = textLength;

                _pending = blocks;
                _pendingLength = textLength;
            }

            if (_typing.IsEnabled)
            {
                return;
            }

            RaiseUpdate();
        }

        protected override MessageContent CreateContent()
        {
            return new MessageRichMessage(new RichMessage(_text, false, true));
        }

        protected override void OnTyping(int length)
        {
            _text = PageBlockStreaming.Substring(_pending, _textLength + length);
            _textLength = PageBlockStreaming.Length(_text);
        }
    }

    public static class PageBlockStreaming
    {
        public static int Length(IList<PageBlock> blocks)
        {
            int total = 0;
            if (blocks != null)
            {
                foreach (var block in blocks)
                {
                    total += Length(block);
                }
            }
            return total;
        }

        public static int Length(PageBlock block)
        {
            return block switch
            {
                null => 0,
                // Text-only blocks
                PageBlockTitle b => Length(b.Title),
                PageBlockSubtitle b => Length(b.Subtitle),
                PageBlockAuthorDate b => Length(b.Author),
                PageBlockHeader b => Length(b.Header),
                PageBlockSubheader b => Length(b.Subheader),
                PageBlockKicker b => Length(b.Kicker),
                PageBlockSectionHeading b => Length(b.Text),
                PageBlockParagraph b => Length(b.Text),
                PageBlockPreformatted b => Length(b.Text),
                PageBlockFooter b => Length(b.Footer),
                PageBlockThinking b => Length(b.Text),
                PageBlockPullQuote b => Length(b.Text) + Length(b.Credit),
                PageBlockMathematicalExpression b => b.Expression?.Length ?? 0,
                // Atomic
                PageBlockAnchor _ => 0,
                PageBlockDivider _ => 1,
                PageBlockChatLink _ => 1,
                //PageBlockThinking _ => 1,
                // Media + caption
                PageBlockAnimation b => 1 + Length(b.Caption),
                PageBlockAudio b => 1 + Length(b.Caption),
                PageBlockPhoto b => 1 + Length(b.Caption),
                PageBlockVideo b => 1 + Length(b.Caption),
                PageBlockVoiceNote b => 1 + Length(b.Caption),
                PageBlockMap b => 1 + Length(b.Caption),
                PageBlockEmbedded b => 1 + Length(b.Caption),
                // Containers
                PageBlockCover b => Length(b.Cover),
                PageBlockList b => LengthListItems(b.Items),
                PageBlockBlockQuote b => Length(b.Blocks) + Length(b.Credit),
                PageBlockCollage b => Length(b.Blocks) + Length(b.Caption),
                PageBlockSlideshow b => Length(b.Blocks) + Length(b.Caption),
                PageBlockEmbeddedPost b => 1 + Length(b.Blocks) + Length(b.Caption),
                PageBlockDetails b => Length(b.Header) + Length(b.Blocks),
                PageBlockTable b => Length(b.Caption) + LengthTableCells(b.Cells),
                PageBlockRelatedArticles b => Length(b.Header) + LengthRelatedArticles(b.Articles),
                _ => 0,
            };
        }

        public static int Length(PageBlockCaption caption)
        {
            return caption == null ? 0 : Length(caption.Text) + Length(caption.Credit);
        }

        public static int Length(RichText rt)
        {
            switch (rt)
            {
                case null: return 0;
                case RichTextPlain p: return p.Text?.Length ?? 0;

                case RichTexts rs:
                    int sum = 0;
                    if (rs.Texts != null)
                    {
                        foreach (var t in rs.Texts) sum += Length(t);
                    }
                    return sum;

                // Leaves
                case RichTextAnchor _: return 0;
                case RichTextIcon _: return 1;
                case RichTextCustomEmoji ce: return ce.AlternativeText?.Length ?? 0;
                case RichTextMathematicalExpression me: return me.Expression?.Length ?? 0;

                // Wrappers (all delegate to inner Text)
                case RichTextBold b: return Length(b.Text);
                case RichTextItalic b: return Length(b.Text);
                case RichTextUnderline b: return Length(b.Text);
                case RichTextStrikethrough b: return Length(b.Text);
                case RichTextSpoiler b: return Length(b.Text);
                case RichTextFixed b: return Length(b.Text);
                case RichTextSubscript b: return Length(b.Text);
                case RichTextSuperscript b: return Length(b.Text);
                case RichTextMarked b: return Length(b.Text);
                case RichTextUrl b: return Length(b.Text);
                case RichTextEmailAddress b: return Length(b.Text);
                case RichTextPhoneNumber b: return Length(b.Text);
                case RichTextMention b: return Length(b.Text);
                case RichTextHashtag b: return Length(b.Text);
                case RichTextCashtag b: return Length(b.Text);
                case RichTextBotCommand b: return Length(b.Text);
                case RichTextMentionName b: return Length(b.Text);
                case RichTextBankCardNumber b: return Length(b.Text);
                case RichTextDateTime b: return Length(b.Text);
                case RichTextReference b: return Length(b.Text);
                case RichTextReferenceLink b: return Length(b.Text);
                case RichTextAnchorLink b: return Length(b.Text);
            }
            return 0;
        }

        private static int LengthListItems(IList<PageBlockListItem> items)
        {
            int total = 0;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item != null) total += Length(item.Blocks);
                }
            }
            return total;
        }

        private static int LengthTableCells(IList<IList<PageBlockTableCell>> rows)
        {
            int total = 0;
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    if (row == null) continue;
                    foreach (var cell in row)
                    {
                        if (cell != null) total += Length(cell.Text);
                    }
                }
            }
            return total;
        }

        private static int LengthRelatedArticles(IList<PageBlockRelatedArticle> articles)
        {
            int total = 0;
            if (articles != null)
            {
                foreach (var a in articles)
                {
                    if (a == null) continue;
                    total += (a.Title?.Length ?? 0)
                          + (a.Description?.Length ?? 0)
                          + (a.Author?.Length ?? 0);
                }
            }
            return total;
        }

        // ============================================================
        // Substring
        // ============================================================

        public static IList<PageBlock> Substring(IList<PageBlock> blocks, int length)
        {
            int remaining = length;
            return SubstringList(blocks, ref remaining);
        }

        private static IList<PageBlock> SubstringList(IList<PageBlock> blocks, ref int remaining)
        {
            var result = new List<PageBlock>();
            if (blocks == null) return result;
            foreach (var block in blocks)
            {
                if (remaining <= 0) break;
                var taken = SubstringBlock(block, ref remaining);
                if (taken != null) result.Add(taken);
            }
            return result;
        }

        private static PageBlock SubstringBlock(PageBlock block, ref int remaining)
        {
            if (block == null || remaining <= 0) return null;

            int blockLength = Length(block);
            if (blockLength <= remaining)
            {
                // Full inclusion: return the original instance, no allocation.
                remaining -= blockLength;
                return block;
            }

            // Partial inclusion: rebuild per block type.
            switch (block)
            {
                // Single-RichText blocks
                case PageBlockTitle b:
                    return new PageBlockTitle(SubstringRichText(b.Title, ref remaining));
                case PageBlockSubtitle b:
                    return new PageBlockSubtitle(SubstringRichText(b.Subtitle, ref remaining));
                case PageBlockAuthorDate b:
                    return new PageBlockAuthorDate(SubstringRichText(b.Author, ref remaining), b.PublishDate);
                case PageBlockHeader b:
                    return new PageBlockHeader(SubstringRichText(b.Header, ref remaining));
                case PageBlockSubheader b:
                    return new PageBlockSubheader(SubstringRichText(b.Subheader, ref remaining));
                case PageBlockKicker b:
                    return new PageBlockKicker(SubstringRichText(b.Kicker, ref remaining));
                case PageBlockSectionHeading b:
                    return new PageBlockSectionHeading(SubstringRichText(b.Text, ref remaining), b.Size);
                case PageBlockParagraph b:
                    return new PageBlockParagraph(SubstringRichText(b.Text, ref remaining));
                case PageBlockPreformatted b:
                    return new PageBlockPreformatted(SubstringRichText(b.Text, ref remaining), b.Language);
                case PageBlockFooter b:
                    return new PageBlockFooter(SubstringRichText(b.Footer, ref remaining));
                case PageBlockThinking b:
                    return new PageBlockThinking(SubstringRichText(b.Text, ref remaining));
                case PageBlockPullQuote b:
                    {
                        var text = SubstringRichText(b.Text, ref remaining) ?? new RichTextPlain("");
                        var credit = SubstringRichText(b.Credit, ref remaining);
                        return new PageBlockPullQuote(text, credit);
                    }
                case PageBlockMathematicalExpression b:
                    {
                        var expr = b.Expression ?? string.Empty;
                        int take = System.Math.Min(remaining, expr.Length);
                        remaining -= take;
                        return new PageBlockMathematicalExpression(expr.Substring(0, take));
                    }

                // Media + caption: consume 1 for the media identity, then process caption.
                case PageBlockAnimation b:
                    {
                        remaining -= 1;
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockAnimation(b.Animation, caption, b.NeedAutoplay, b.HasSpoiler);
                    }
                case PageBlockAudio b:
                    {
                        remaining -= 1;
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockAudio(b.Audio, caption);
                    }
                case PageBlockPhoto b:
                    {
                        remaining -= 1;
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockPhoto(b.Photo, caption, b.Url, b.HasSpoiler);
                    }
                case PageBlockVideo b:
                    {
                        remaining -= 1;
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockVideo(b.Video, caption, b.NeedAutoplay, b.IsLooped, b.HasSpoiler);
                    }
                case PageBlockVoiceNote b:
                    {
                        remaining -= 1;
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockVoiceNote(b.VoiceNote, caption);
                    }
                case PageBlockMap b:
                    {
                        remaining -= 1;
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockMap(b.Location, b.Zoom, b.Width, b.Height, caption);
                    }
                case PageBlockEmbedded b:
                    {
                        remaining -= 1;
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockEmbedded(b.Url, b.Html, b.PosterPhoto, b.Width, b.Height,
                            caption, b.IsFullWidth, b.AllowScrolling);
                    }

                // Containers
                case PageBlockCover b:
                    {
                        var inner = SubstringBlock(b.Cover, ref remaining);
                        return inner == null ? null : new PageBlockCover(inner);
                    }
                case PageBlockList b:
                    return new PageBlockList(SubstringListItems(b.Items, ref remaining));
                case PageBlockBlockQuote b:
                    {
                        var inner = SubstringList(b.Blocks, ref remaining);
                        var credit = SubstringRichText(b.Credit, ref remaining);
                        return new PageBlockBlockQuote(inner, credit);
                    }
                case PageBlockCollage b:
                    {
                        var inner = SubstringList(b.Blocks, ref remaining);
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockCollage(inner, caption);
                    }
                case PageBlockSlideshow b:
                    {
                        var inner = SubstringList(b.Blocks, ref remaining);
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockSlideshow(inner, caption);
                    }
                case PageBlockEmbeddedPost b:
                    {
                        remaining -= 1; // post identity
                        var inner = SubstringList(b.Blocks, ref remaining);
                        var caption = SubstringCaption(b.Caption, ref remaining);
                        return new PageBlockEmbeddedPost(b.Url, b.Author, b.AuthorPhoto, b.Date, inner, caption);
                    }
                case PageBlockDetails b:
                    {
                        var header = SubstringRichText(b.Header, ref remaining) ?? new RichTextPlain("");
                        var inner = SubstringList(b.Blocks, ref remaining);
                        return new PageBlockDetails(header, inner, b.IsOpen);
                    }
                case PageBlockTable b:
                    {
                        // Cells first (content), then caption.
                        var cells = SubstringTableCells(b.Cells, ref remaining);
                        var caption = SubstringRichText(b.Caption, ref remaining);
                        return new PageBlockTable(caption, cells, b.IsBordered, b.IsStriped);
                    }
                case PageBlockRelatedArticles b:
                    {
                        var header = SubstringRichText(b.Header, ref remaining) ?? new RichTextPlain("");
                        var articles = SubstringRelatedArticles(b.Articles, ref remaining);
                        return new PageBlockRelatedArticles(header, articles);
                    }
            }

            return null;
        }

        private static RichText SubstringRichText(RichText rt, ref int remaining)
        {
            if (rt == null || remaining <= 0) return null;

            int len = Length(rt);
            if (len <= remaining)
            {
                remaining -= len;
                return rt; // include whole
            }

            switch (rt)
            {
                case RichTextPlain p:
                    {
                        var text = p.Text ?? string.Empty;
                        int take = System.Math.Min(remaining, text.Length);
                        remaining -= take;
                        return new RichTextPlain(text.Substring(0, take));
                    }

                case RichTexts rs:
                    {
                        var children = new List<RichText>();
                        if (rs.Texts != null)
                        {
                            foreach (var t in rs.Texts)
                            {
                                if (remaining <= 0) break;
                                var sub = SubstringRichText(t, ref remaining);
                                if (sub != null) children.Add(sub);
                            }
                        }
                        return new RichTexts(children);
                    }

                case RichTextMathematicalExpression me:
                    {
                        var expr = me.Expression ?? string.Empty;
                        int take = System.Math.Min(remaining, expr.Length);
                        remaining -= take;
                        return new RichTextMathematicalExpression(expr.Substring(0, take));
                    }

                // Atomic, no partial form.
                case RichTextIcon _:
                case RichTextCustomEmoji _:
                    return null;

                // Wrappers without metadata
                case RichTextBold b:
                    return new RichTextBold(SubstringRichText(b.Text, ref remaining));
                case RichTextItalic b:
                    return new RichTextItalic(SubstringRichText(b.Text, ref remaining));
                case RichTextUnderline b:
                    return new RichTextUnderline(SubstringRichText(b.Text, ref remaining));
                case RichTextStrikethrough b:
                    return new RichTextStrikethrough(SubstringRichText(b.Text, ref remaining));
                case RichTextSpoiler b:
                    return new RichTextSpoiler(SubstringRichText(b.Text, ref remaining));
                case RichTextFixed b:
                    return new RichTextFixed(SubstringRichText(b.Text, ref remaining));
                case RichTextSubscript b:
                    return new RichTextSubscript(SubstringRichText(b.Text, ref remaining));
                case RichTextSuperscript b:
                    return new RichTextSuperscript(SubstringRichText(b.Text, ref remaining));
                case RichTextMarked b:
                    return new RichTextMarked(SubstringRichText(b.Text, ref remaining));

                // Wrappers with metadata
                case RichTextMention b:
                    return new RichTextMention(SubstringRichText(b.Text, ref remaining), b.Username);
                case RichTextHashtag b:
                    return new RichTextHashtag(SubstringRichText(b.Text, ref remaining), b.Hashtag);
                case RichTextCashtag b:
                    return new RichTextCashtag(SubstringRichText(b.Text, ref remaining), b.Cashtag);
                case RichTextBotCommand b:
                    return new RichTextBotCommand(SubstringRichText(b.Text, ref remaining), b.BotCommand);
                case RichTextBankCardNumber b:
                    return new RichTextBankCardNumber(SubstringRichText(b.Text, ref remaining), b.BankCardNumber);
                case RichTextUrl b:
                    return new RichTextUrl(SubstringRichText(b.Text, ref remaining), b.Url, b.IsCached);
                case RichTextEmailAddress b:
                    return new RichTextEmailAddress(SubstringRichText(b.Text, ref remaining), b.EmailAddress);
                case RichTextPhoneNumber b:
                    return new RichTextPhoneNumber(SubstringRichText(b.Text, ref remaining), b.PhoneNumber);
                case RichTextMentionName b:
                    return new RichTextMentionName(SubstringRichText(b.Text, ref remaining), b.UserId);
                case RichTextDateTime b:
                    return new RichTextDateTime(SubstringRichText(b.Text, ref remaining), b.UnixTime, b.FormattingType);
                case RichTextReference b:
                    return new RichTextReference(b.Name, SubstringRichText(b.Text, ref remaining));
                case RichTextReferenceLink b:
                    return new RichTextReferenceLink(SubstringRichText(b.Text, ref remaining), b.ReferenceName, b.Url);
                case RichTextAnchorLink b:
                    return new RichTextAnchorLink(SubstringRichText(b.Text, ref remaining), b.AnchorName, b.Url);
            }

            return null;
        }

        private static PageBlockCaption SubstringCaption(PageBlockCaption caption, ref int remaining)
        {
            if (caption == null)
            {
                return new PageBlockCaption(new RichTextPlain(string.Empty), new RichTextPlain(string.Empty));
            }
            var text = SubstringRichText(caption.Text, ref remaining) ?? new RichTextPlain(string.Empty);
            var credit = SubstringRichText(caption.Credit, ref remaining) ?? new RichTextPlain(string.Empty);
            return new PageBlockCaption(text, credit);
        }

        private static IList<PageBlockListItem> SubstringListItems(IList<PageBlockListItem> items, ref int remaining)
        {
            var result = new List<PageBlockListItem>();
            if (items == null) return result;
            foreach (var item in items)
            {
                if (remaining <= 0) break;
                if (item == null) continue;
                int itemLen = Length(item.Blocks);
                if (itemLen == 0)
                {
                    // Empty item — skip so we don't emit bullet markers without content.
                    continue;
                }
                if (itemLen <= remaining)
                {
                    remaining -= itemLen;
                    result.Add(item);
                }
                else
                {
                    var sub = SubstringList(item.Blocks, ref remaining);
                    result.Add(new PageBlockListItem(item.Label, sub, item.HasCheckbox, item.IsChecked, item.Value, item.Type));
                }
            }
            return result;
        }

        private static IList<IList<PageBlockTableCell>> SubstringTableCells(IList<IList<PageBlockTableCell>> rows, ref int remaining)
        {
            var result = new List<IList<PageBlockTableCell>>();
            if (rows == null) return result;
            foreach (var row in rows)
            {
                if (remaining <= 0) break;
                if (row == null) continue;
                var newRow = new List<PageBlockTableCell>();
                foreach (var cell in row)
                {
                    if (remaining <= 0) break;
                    if (cell == null) continue;
                    int cellLen = Length(cell.Text);
                    if (cellLen <= remaining)
                    {
                        remaining -= cellLen;
                        newRow.Add(cell);
                    }
                    else
                    {
                        var text = SubstringRichText(cell.Text, ref remaining) ?? new RichTextPlain(string.Empty);
                        newRow.Add(new PageBlockTableCell(text, cell.IsHeader, cell.Colspan, cell.Rowspan, cell.Align, cell.Valign));
                    }
                }
                if (newRow.Count > 0) result.Add(newRow);
            }
            return result;
        }

        private static IList<PageBlockRelatedArticle> SubstringRelatedArticles(IList<PageBlockRelatedArticle> articles, ref int remaining)
        {
            var result = new List<PageBlockRelatedArticle>();
            if (articles == null) return result;
            foreach (var a in articles)
            {
                if (remaining <= 0) break;
                if (a == null) continue;
                int aLen = (a.Title?.Length ?? 0) + (a.Description?.Length ?? 0) + (a.Author?.Length ?? 0);
                if (aLen <= remaining)
                {
                    remaining -= aLen;
                    result.Add(a);
                }
                else
                {
                    string title = TakeString(a.Title, ref remaining);
                    string description = TakeString(a.Description, ref remaining);
                    string author = TakeString(a.Author, ref remaining);
                    result.Add(new PageBlockRelatedArticle(a.Url, title, description, a.Photo, author, a.PublishDate));
                }
            }
            return result;
        }

        private static string TakeString(string s, ref int remaining)
        {
            if (string.IsNullOrEmpty(s) || remaining <= 0) return string.Empty;
            int take = System.Math.Min(s.Length, remaining);
            remaining -= take;
            return take == s.Length ? s : s.Substring(0, take);
        }
    }
}
