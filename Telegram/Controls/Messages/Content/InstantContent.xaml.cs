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
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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
            _renderer = new PageBlockRenderer(this);

            DefaultStyleKey = typeof(InstantContent);
            Telegram.Common.Instrumentation.Register(this);
        }

        public InstantContent()
        {
            _renderer = new PageBlockRenderer(this);

            DefaultStyleKey = typeof(InstantContent);
        }

#if INSTRUMENTATION
        // The instrumented controls (text blocks + media content controls) this InstantContent
        // currently holds. They're nested arbitrarily deep, so one panel's Children won't do.
        //
        // Walked rather than accumulated as they're created: the diff replaces elements on every
        // edit of a streaming message, and a list of everything ever built both keeps the discarded
        // ones alive and reports them as still reachable -- which is exactly the orphan the analysis
        // exists to find.
        internal IEnumerable<object> DebugChildren()
        {
            return LayoutRoot != null ? Descendants(LayoutRoot) : Array.Empty<object>();

            static IEnumerable<object> Descendants(DependencyObject parent)
            {
                var count = Windows.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);

                for (int i = 0; i < count; i++)
                {
                    var child = Windows.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);

                    if (child is FormattedTextBlock or IContent)
                    {
                        yield return child;
                    }

                    foreach (var nested in Descendants(child))
                    {
                        yield return nested;
                    }
                }
            }
        }
#endif

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

        public FormattedTextBlock LastBlock
        {
            get
            {
                return FindBlock(LayoutRoot);

                static FormattedTextBlock FindBlock(UIElement element)
                {
                    if (element is Panel panel && panel.Children.Count > 0)
                    {
                        // TODO: a better logic is needed (i.e. only use for some specific panel type)
                        return FindBlock(panel.Children[^1]);
                    }
                    else if (element is FormattedTextBlock block && block.TextAlignment == TextAlignment.DetectFromContent)
                    {
                        return block;
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

        private RichMessage GetContent(MessageViewModel message)
        {
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
                UpdateView(message.ClientService, linkPreview.Blocks, !linkPreview.IsFull);
            }


            if (!linkPreview.IsFull)
            {
                var load = new ButtonEx();
                load.Style = BootStrapper.Current.Resources["InstantViewButtonStyle"] as Style;
                load.Content = "Show more";
                load.Margin = new Thickness(10, 8, 10, 4);
                load.Click += async (s, args) =>
                {
                    load.ShowSkeleton();

                    var response = await _message.ClientService.SendAsync(new GetFullRichMessage(message.ChatId, message.Id));
                    if (response is RichMessage richMessage && /*instantView.IsFull &&*/ !token.IsCancellationRequested)
                    {
                        _message.Delegate.NavigationService.NavigateToInstant(new WebPageInstantView(richMessage.Blocks, 0, 2, richMessage.IsRtl, richMessage.IsFull, null), "tg://test");
                    }

                    load.HideSkeleton();
                };

                LayoutRoot.Children.Add(load);
            }
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

        void IPageBlockContext.TextEntityClick(FormattedTextBlock sender, TextEntityClickEventArgs args)
        {
            MessageBubble.TextEntityClick(_message, sender, args);
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
