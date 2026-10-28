//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.RichEditor;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class InstantContent : ControlEx, IContent, IPageBlockContext
    {
        private CancellationTokenSource _instantViewToken;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private RichMessageDelegate _delegate;

        public InstantContent(MessageViewModel message)
        {
            _message = message;
            _renderer = new PageBlockRenderer(this)
            {
                HorizontalPadding = 10
            };

            DefaultStyleKey = typeof(InstantContent);
        }

        public InstantContent()
        {
            _renderer = new PageBlockRenderer(this)
            {
                HorizontalPadding = 10
            };

            DefaultStyleKey = typeof(InstantContent);
        }

        #region InitializeComponent

        private StackPanel LayoutRoot;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as StackPanel;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
            else if (_prevValue != null)
            {
                var blocks = _prevValue;
                _prevValue = null;
                UpdateView(_clientService, blocks, false);
            }
        }

        #endregion

        /// <summary>
        /// The block the message footer may share a line with: the article's last block, and only
        /// when the block is a paragraph in its own right.
        /// </summary>
        /// <remarks>
        /// Not the last text block anywhere in the article. One nested in a quote, a list, a
        /// details or a table cell is laid out inside that panel, with its own width and its own
        /// insets, and the panel goes on below it - so the end of its last line says nothing about
        /// where the article ends. Only the blocks <c>PageBlockRenderer.ProcessText</c> renders
        /// reach the root as a bare text block, which is exactly the set that can be shared.
        /// Centered ones are left out: the footer would land in the gap beside the text rather
        /// than after it.
        /// </remarks>
        public ITextPresenter LastBlock
        {
            get
            {
                return FindBlock(LayoutRoot);

                static ITextPresenter FindBlock(UIElement element)
                {
                    if (element is FormattedTextBlock block && block.AdjustLineEnding)
                    {
                        return block;
                    }
                    else if (element is DirectTextBlock direct && direct.AdjustLineEnding)
                    {
                        return direct;
                    }
                    else if (element is Panel panel && panel.Children.Count > 0)
                    {
                        // TODO: a better logic is needed (i.e. only use for some specific panel type)
                        return FindBlock(panel.Children[^1]);
                    }

                    return null;
                }
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _instantViewToken?.Cancel();
            _instantViewToken = new CancellationTokenSource();

            _message = message;

            var text = GetContent(message);
            if (text == null || !_templateApplied)
            {
                return;
            }

            _delegate = new RichMessageDelegate(text, message.Delegate as DialogMessageDelegate);
            UpdateInstantView(message, text, _instantViewToken.Token);
        }

        public void UpdateChatDraft(DialogMessageDelegate delegato, DraftMessageContentRichMessage richMessage)
        {
            _delegate = new RichMessageDelegate(richMessage.Message, delegato);
            UpdateView(delegato.ClientService, richMessage.Message.Blocks, false);
        }

        public void Recycle()
        {
            _instantViewToken?.Cancel();
            _message = null;

            //if (_templateApplied && Media.Child is IContent content)
            //{
            //    content.Recycle();
            //}
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageRichMessage;
        }

        /// <summary>
        /// A reply quote from a selection in this message, as the message's own rich texts. False
        /// when the selection covers nothing quotable, or when what is shown is not the message
        /// itself - a translation is not what the server checks the quote against.
        /// </summary>
        public bool TryGetQuote(IReadOnlyList<TextSelectionRange> selection, long lengthMax, out Vector<RichText> texts, out int position)
        {
            texts = null;
            position = 0;

            if (_message?.Content is not MessageRichMessage richMessage || _message.TranslatedText is MessageTranslateResultRichMessage || selection == null)
            {
                return false;
            }

            var ranges = new List<RichQuoteRange>(selection.Count);

            foreach (var range in selection)
            {
                if (range.Item.QuoteSource is object source)
                {
                    ranges.Add(new RichQuoteRange(source, range.Item.GetSourceOffset(range.Start), range.Item.GetSourceOffset(range.End)));
                }
            }

            return PageBlockHelper.TryGetQuote(richMessage.Message.Blocks, ranges, lengthMax, out texts, out position);
        }

        /// <summary>
        /// A selection in this message as the blocks it covers, for copy: of what is shown, so
        /// of a translation when one is. Null when it covers none of the message's texts.
        /// </summary>
        public RichDocument GetSelectionFragment(IReadOnlyList<TextSelectionRange> selection)
        {
            var content = _message != null ? GetContent(_message) : null;
            if (content == null || selection == null)
            {
                return null;
            }

            var ranges = new List<RichQuoteRange>(selection.Count);

            foreach (var range in selection)
            {
                if (range.Item.QuoteSource is object source)
                {
                    ranges.Add(new RichQuoteRange(source, range.Item.GetSourceOffset(range.Start), range.Item.GetSourceOffset(range.End)));
                }
            }

            return RichDocumentConverter.FromSelection(content.Blocks, PageBlockHelper.GetSelectionRanges(content.Blocks, ranges));
        }

        /// <summary>
        /// The lines a quote of this message covers, in this control's coordinates, for the
        /// highlight shown on following a reply to it. Null when it is not found in what is shown.
        /// </summary>
        public IList<Rect> GetQuoteRectangles(TextQuote quote)
        {
            if (_message?.Content is not MessageRichMessage richMessage || _message.TranslatedText is MessageTranslateResultRichMessage || LayoutRoot == null)
            {
                return null;
            }

            var ranges = new List<RichQuoteRange>();
            if (!PageBlockHelper.TryFindQuote(richMessage.Message.Blocks, quote.RichTexts, quote.Position, ranges))
            {
                return null;
            }

            var controls = new List<ISelectableControl>();
            CollectQuoteControls(LayoutRoot, controls);

            // Equal texts only when none is the text itself: an element kept across an edit
            // points to the text before it, but an exact match never lights up a twin.
            return GetQuoteRectangles(controls, ranges, true) ?? GetQuoteRectangles(controls, ranges, false);
        }

        private List<Rect> GetQuoteRectangles(List<ISelectableControl> controls, List<RichQuoteRange> ranges, bool exact)
        {
            List<Rect> result = null;

            foreach (var control in controls)
            {
                if (!PageBlockHelper.TryGetQuoteRange(control.QuoteSource, ranges, exact, out int start, out int end))
                {
                    continue;
                }

                var rects = control.GetHighlightRectangles(start, end);
                if (rects == null)
                {
                    continue;
                }

                var offset = ((UIElement)control).TransformToPoint(this);

                foreach (var rect in rects)
                {
                    (result ??= new List<Rect>()).Add(new Rect(rect.X + offset.X, rect.Y + offset.Y, rect.Width, rect.Height));
                }
            }

            return result;
        }

        // What is collapsed is not shown, so there is nothing of it to highlight.
        private static void CollectQuoteControls(DependencyObject node, List<ISelectableControl> controls)
        {
            var count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);

                if (child is FrameworkElement { Visibility: Visibility.Collapsed })
                {
                    continue;
                }
                else if (child is ISelectableControl control)
                {
                    if (control.QuoteSource != null)
                    {
                        controls.Add(control);
                    }

                    continue;
                }

                CollectQuoteControls(child, controls);
            }
        }

        private RichMessage GetContent(MessageViewModel message)
        {
            if (message.TranslatedText is MessageTranslateResultRichMessage richMessage)
            {
                return richMessage.RichMessage;
            }

            var content = message?.GeneratedContent ?? message?.Content;
            if (content is MessageRichMessage text)
            {
                return text.Message;
            }

            return null;
        }


        private async void UpdateInstantView(MessageViewModel message, RichMessage linkPreview, CancellationToken token)
        {
            //var response = await _message.ClientService.SendAsync(new GetFullRichMessage(message.ChatId, message.Id));
            //if (response is RichMessage richMessage && /*instantView.IsFull &&*/ !token.IsCancellationRequested)
            {
                var processedText = message.SummarizedText ?? message.TranslatedText;

                ShowHideSkeleton(processedText is MessageTranslateResultPending);
                UpdateView(message.ClientService, linkPreview.Blocks, !linkPreview.IsFull);
            }

            ShowHideMore(!linkPreview.IsFull);
        }

        private ButtonEx _showMore;

        private void ShowHideMore(bool show)
        {
            if (_showMore != null == show)
            {
                return;
            }

            if (_showMore == null)
            {
                _showMore = new ButtonEx();
                _showMore.Style = BootStrapper.Current.Resources["InstantViewButtonStyle"] as Style;
                _showMore.Content = Strings.ShowMore;
                _showMore.Margin = new Thickness(10, 8, 10, 4);
                _showMore.Click += ShowMore_Click;

                LayoutRoot.Children.Add(_showMore);
            }
            else
            {
                LayoutRoot.Children.Remove(_showMore);
            }
        }

        private async void ShowMore_Click(object sender, RoutedEventArgs e)
        {
            var message = _message;
            if (message == null)
            {
                return;
            }

            var showMore = _showMore;
            var token = _instantViewToken.Token;

            showMore?.ShowSkeleton();

            var response = await message.ClientService.SendAsync(new GetFullRichMessage(message.ChatId, message.Id));
            if (response is RichMessage richMessage && /*instantView.IsFull &&*/ !token.IsCancellationRequested)
            {
                message.Delegate.NavigationService.NavigateToInstant(new WebPageInstantView(richMessage.Blocks, 0, 2, richMessage.IsRtl, richMessage.IsFull, null), "tg://test");
            }

            showMore?.HideSkeleton();
        }

        private bool _skeletonCollapsed = true;

        public void ShowHideSkeleton(bool show)
        {
            _skeletonCollapsed = !show;
        }

        private IClientService _clientService;
        private Vector<PageBlock> _prevValue;

        public void UpdateView(IClientService clientService, Vector<PageBlock> blocks, bool part)
        {
            // Kept for the whole lifetime, not just while the template is pending: the
            // renderer asks for a message back (IPageBlockContext.CreateMessage) outside
            // of any UpdateView call, and this control can render without a message at all.
            _clientService = clientService;

            if (!_templateApplied)
            {
                _prevValue = blocks;
                return;
            }

            var prev = _prevValue ?? Array.Empty<PageBlock>();
            var diff = DiffCalculator.Create(prev, blocks, PageBlockHelper.Compare);

            var added = 0;
            var removed = 0;
            var moved = 0;

            while (diff.Next())
            {
                if (diff.State == DiffState.Add)
                {
                    added++;

                    var element = _renderer.ProcessBlock(clientService, diff.NewValue, null);
                    if (element != null)
                    {
                        LayoutRoot.Children.Insert(diff.NewIndex, element);
                    }
                    else
                    {
                        LayoutRoot.Children.Insert(diff.NewIndex, new Border());
                    }

                    //UpdateItem(diff.NewValue, null, diff.NewIndex);
                }
                else if (diff.State == DiffState.Move && diff.OldIndex < LayoutRoot.Children.Count && diff.NewIndex < LayoutRoot.Children.Count)
                {
                    moved++;

                    //UpdateItem(diff.OldValue, diff.NewValue);
                    LayoutRoot.Children.Move((uint)diff.OldIndex, (uint)diff.NewIndex);
                }
                else if (diff.State == DiffState.Remove && diff.OldIndex < LayoutRoot.Children.Count)
                {
                    //if (diff.OldValue is MessageReaction oldReaction)
                    //{
                    //    _cache.Remove(oldReaction.Type);
                    //}

                    removed++;

                    // Before the remove, while it is still in the tree to be walked.
                    _renderer.Release(LayoutRoot.Children[diff.OldIndex]);

                    LayoutRoot.Children.RemoveAt(diff.OldIndex);

                    if (diff.OldValue is PageBlockAnchor anchor)
                    {
                        _renderer.RemoveAnchor(anchor.Name);
                    }
                }
            }

            Logger.Info(string.Format("Added: {0}, removed: {1}, moved: {2}", added, removed, moved));

            _renderer.UpdateSpacing(LayoutRoot, blocks, true);

            _prevValue = blocks;
        }

        #region IPageBlockContext

        // The renderer builds every block; these are the parts only the host can
        // answer. A rich message has a real message behind it, so links route to the
        // bubble and buttons can actually be answered.
        private readonly PageBlockRenderer _renderer;

        ResourceDictionary IPageBlockContext.Resources => LayoutRoot.Resources;

        bool IPageBlockContext.IsConnected => IsConnected;

        bool IPageBlockContext.IsSkeletonVisible => !_skeletonCollapsed;

        MessageViewModel IPageBlockContext.CreateMessage(long id, MessageContent content)
        {
            return new MessageViewModel(_clientService ?? _message?.ClientService, _delegate, _message?.Chat, null, null, new Message { Id = id, Content = content, SchedulingState = new MessageSchedulingStateSendWhenOnline() });
        }

        void IPageBlockContext.TextEntityClick(TextEntityClickEventArgs args)
        {
            MessageBubble.TextEntityClick(_message, args);
        }

        void IPageBlockContext.OpenUrl(string url)
        {
            MessageHelper.OpenUrl(_clientService ?? _message?.ClientService, _message?.Delegate?.NavigationService, url);
        }

        void IPageBlockContext.OpenInlineButton(InlineButton button)
        {
            if (_message == null)
            {
                return;
            }

            // Re-shape the IV button into the keyboard button the existing handler
            // expects — same style and type, only the label's RichText flattens to a
            // string. Unlike an instant view, a message can answer every type: it has
            // the chat and message id a callback query needs.
            var inline = new InlineKeyboardButton(button.Text.ToPlainText() ?? string.Empty, 0, button.Style, button.Type);
            _message.Delegate?.OpenInlineButton(_message, inline);
        }

        #endregion
    }
}
