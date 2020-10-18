using System;
using System.Numerics;
using System.Windows.Input;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls.Messages
{
    public sealed partial class MessagePinned : Grid
    {
        private UIElement _parent;

        private Visual _visual1;
        private Visual _visual2;

        private Visual _visual;

        private long _chatId;
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

        public void InitializeParent(UIElement parent)
        {
            _parent = parent;
        }

        public void UpdateMessage(Chat chat, long messageId, MessageViewModel message, bool loading, string title = null)
        {
            if (message == null && !loading)
            {
                _chatId = 0;
                _messageId = 0;

                _loading = false;

                ShowHide(false);
                return;
            }

            ShowHide(true);

            if (_chatId == chat.Id && _messageId == messageId)
            {
                if (_loading)
                {
                    var referenceShown = _visual == _visual1 ? Reference1 : Reference2;
                    referenceShown.UpdateMessage(message, loading, title);

                    _loading = loading;
                }

                return;
            }

            var cross = _chatId == chat.Id;
            var prev = _messageId < messageId;

            _chatId = chat.Id;
            _messageId = messageId;

            _loading = loading;

            var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            var referenceShow = _visual == _visual1 ? Reference2 : Reference1;
            var referenceHide = _visual == _visual1 ? Reference1 : Reference2;

            Canvas.SetZIndex(referenceShow, 1);
            Canvas.SetZIndex(referenceHide, 0);

            if (cross)
            {
                var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
                hide1.InsertKeyFrame(0, new Vector3(0));
                hide1.InsertKeyFrame(1, new Vector3(0, prev ? -32 : 32, 0));

                visualHide.StartAnimation("Offset", hide1);
            }
            else
            {
                visualHide.Offset = Vector3.Zero;
            }

            var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Opacity", hide2);

            referenceShow.UpdateMessage(message, loading, title);
            referenceShow.IsTabStop = true;
            referenceHide.IsTabStop = false;

            if (cross)
            {
                var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
                show1.InsertKeyFrame(0, new Vector3(0, prev ? 32 : -32, 0));
                show1.InsertKeyFrame(1, new Vector3(0));

                visualShow.StartAnimation("Offset", show1);
            }
            else
            {
                visualShow.Offset = Vector3.Zero;
            }

            var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(0, 0);
            show2.InsertKeyFrame(1, 1);

            visualShow.StartAnimation("Opacity", show2);

            _visual = visualShow;
        }

        private bool _collapsed = true;

        private void ShowHide(bool show)
        {
            if ((show && Visibility == Visibility.Visible) || (!show && (Visibility == Visibility.Collapsed || _collapsed)))
            {
                return;
            }

            if (show)
            {
                _collapsed = false;
            }
            else
            {
                _collapsed = true;
            }

            Visibility = Visibility.Visible;

            var visual = ElementCompositionPreview.GetElementVisual(_parent);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                visual.Offset = new Vector3();

                if (show)
                {
                    _collapsed = false;
                }
                else
                {
                    Visibility = Visibility.Collapsed;
                }
            };

            var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
            clip.InsertKeyFrame(show ? 0 : 1, 48);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = TimeSpan.FromMilliseconds(150);

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -48, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = TimeSpan.FromMilliseconds(150);

            visual.Clip.StartAnimation("TopInset", clip);
            visual.StartAnimation("Offset", offset);

            batch.End();
        }

        public ICommand HideCommand
        {
            get => HideButton.Command;
            set => HideButton.Command = value;
        }

        public event RoutedEventHandler Click;

        private void Reference_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
        }
    }
}
