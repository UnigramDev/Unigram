using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls.Messages
{
    public sealed partial class MessagePinned : Grid
    {
        private Visual _visual1;
        private Visual _visual2;

        private Visual _visual;

        private long _chatId;
        private long _chatOrder;
        private long _messageId;

        private bool _loading;

        public MessagePinned()
        {
            InitializeComponent();

            var root = ElementCompositionPreview.GetElementVisual(this);
            root.Clip = root.Compositor.CreateInsetClip();

            _visual1 = ElementCompositionPreview.GetElementVisual(Reference1);
            _visual2 = ElementCompositionPreview.GetElementVisual(Reference2);

            _visual = _visual1;
        }

        public void UpdateMessage(Chat chat, long messageId, MessageViewModel message, bool loading)
        {
            if (_chatId == chat.Id && _messageId == messageId)
            {
                if (_loading)
                {
                    var referenceShown = _visual == _visual1 ? Reference1 : Reference2;
                    referenceShown.UpdateMessage(message, loading, Strings.Resources.PinnedMessage);

                    _loading = false;
                }

                return;
            }

            var order = chat.GetOrder(null);
            var prev = _chatOrder < order || (_chatId == chat.Id && _messageId > messageId);

            _chatId = chat.Id;
            _chatOrder = order;
            _messageId = messageId;

            _loading = loading;

            var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            var referenceShow = _visual == _visual1 ? Reference2 : Reference1;

            var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(0, new Vector3(0));
            hide1.InsertKeyFrame(1, new Vector3(0, prev ? -32 : 32, 0));

            var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Offset", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            referenceShow.UpdateMessage(message, loading, Strings.Resources.PinnedMessage);

            var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(0, new Vector3(0, prev ? 32 : -32, 0));
            show1.InsertKeyFrame(1, new Vector3(0));

            var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(0, 0);
            show2.InsertKeyFrame(1, 1);
            
            visualShow.StartAnimation("Offset", show1);
            visualShow.StartAnimation("Opacity", show2);

            _visual = visualShow;
        }

        public event RoutedEventHandler Click;

        private void Reference_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
        }
    }
}
