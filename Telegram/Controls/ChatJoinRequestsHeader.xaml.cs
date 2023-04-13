//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public sealed partial class ChatJoinRequestsHeader : HyperlinkButton
    {
        private IClientService _clientService;

        private UIElement _parent;
        private Chat _chat;

        public ChatJoinRequestsHeader()
        {
            InitializeComponent();
        }

        public void InitializeParent(UIElement parent, IClientService clientService)
        {
            _clientService = clientService;

            _parent = parent;
            ElementCompositionPreview.SetIsTranslationEnabled(parent, true);
        }

        private void RecentUsers_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            if (_clientService.TryGetUser(messageSender, out User user))
            {
                sender.SetUser(_clientService, user, 32);
            }
            else if (_clientService.TryGetChat(messageSender, out Chat chat))
            {
                sender.SetChat(_clientService, chat, 32);
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
                    _clientService.Send(new GetChatJoinRequests(chat.Id, string.Empty, string.Empty, null, 3));
                }

                Label.Text = Locale.Declension(Strings.R.JoinRequests, chat.PendingJoinRequests.TotalCount);

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
            clip.Duration = Constants.FastAnimation;

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            offset.Duration = Constants.FastAnimation;

            visual.Clip.StartAnimation("TopInset", clip);
            parent.StartAnimation("Translation", offset);

            batch.End();
        }
    }
}
