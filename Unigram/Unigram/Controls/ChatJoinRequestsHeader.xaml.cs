using System;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls
{
    public sealed partial class ChatJoinRequestsHeader : HyperlinkButton
    {
        private IProtoService _protoService;

        private UIElement _parent;
        private Chat _chat;

        public ChatJoinRequestsHeader()
        {
            InitializeComponent();
        }

        public void InitializeParent(UIElement parent, IProtoService protoService)
        {
            _protoService = protoService;

            _parent = parent;
            ElementCompositionPreview.SetIsTranslationEnabled(parent, true);
        }

        private void RecentUsers_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            if (_protoService.TryGetUser(messageSender, out User user))
            {
                sender.SetUser(_protoService, user, 32);
            }
            else if (_protoService.TryGetChat(messageSender, out Chat chat))
            {
                sender.SetChat(_protoService, chat, 32);
            }
        }

        public bool UpdateChat(Chat chat)
        {
            var visible = true;
            var channel = chat.Type is ChatTypeSupergroup super && super.IsChannel;

            if (chat.PendingJoinRequests?.TotalCount > 0)
            {
                ShowHide(true);

                if (chat.PendingJoinRequests.UserIds.Count < 3 
                    && chat.PendingJoinRequests.UserIds.Count < chat.PendingJoinRequests.TotalCount)
                {
                    _protoService.Send(new GetChatJoinRequests(chat.Id, string.Empty, string.Empty, null, 3));
                }

                Label.Text = Locale.Declension("JoinRequests", chat.PendingJoinRequests.TotalCount);

                var destination = RecentUsers.Items;
                var origin = chat.PendingJoinRequests.UserIds;

                if (destination.Count > 0 && _chat?.Id == chat.Id)
                {
                    destination.ReplaceDiff(origin.Select(x => new MessageSenderUser(x)));
                }
                else
                {
                    destination.Clear();
                    destination.AddRange(origin.Select(x => new MessageSenderUser(x)));
                }
            }
            else
            {
                ShowHide(false);
                visible = false;
            }

            _chat = chat;
            return visible;
        }

        private bool _collapsed = true;

        public void ShowHide(bool show)
        {
            if (_collapsed != show)
            {
                return;
            }

            _collapsed = !show;
            Visibility = Visibility.Visible;

            var parent = ElementCompositionPreview.GetElementVisual(_parent);
            var visual = ElementCompositionPreview.GetElementVisual(this);
            visual.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;
                parent.Properties.InsertVector3("Translation", Vector3.Zero);

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
            clip.InsertKeyFrame(show ? 0 : 1, 40);
            clip.InsertKeyFrame(show ? 1 : 0, 0);
            clip.Duration = TimeSpan.FromMilliseconds(150);

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = TimeSpan.FromMilliseconds(150);

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }
    }
}
