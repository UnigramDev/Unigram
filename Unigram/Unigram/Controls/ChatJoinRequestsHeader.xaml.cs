using System;
using System.Linq;
using System.Numerics;
using System.Windows.Input;
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

            RecentUsers.GetPicture = sender =>
            {
                if (protoService.TryGetUser(sender, out User user))
                {
                    return PlaceholderHelper.GetUser(protoService, user, 32);
                }
                else if (protoService.TryGetChat(sender, out Chat chat))
                {
                    return PlaceholderHelper.GetChat(protoService, chat, 32);
                }

                return null;
            };
        }

        public bool UpdateChat(Chat chat)
        {
            var visible = true;
            var channel = chat.Type is ChatTypeSupergroup super && super.IsChannel;

            //if (chat.VideoChat.GroupCallId != call?.Id || !chat.VideoChat.HasParticipants || call == null || call.IsJoined)
            //{
            //    ShowHide(false);
            //    visible = false;
            //}
            //else
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

                if (_chat?.Id == chat.Id)
                {
                    for (int i = 0; i < origin.Count; i++)
                    {
                        var item = origin[i];
                        var index = -1;

                        for (int j = 0; j < destination.Count; j++)
                        {
                            if (destination[j] is MessageSenderUser senderUser && senderUser.UserId == item)
                            {
                                index = j;
                                break;
                            }
                        }

                        if (index > -1 && index != i)
                        {
                            destination.Move(index, Math.Min(i, destination.Count));
                        }
                        else if (index == -1)
                        {
                            destination.Insert(Math.Min(i, destination.Count), new MessageSenderUser(item));
                        }
                    }

                    for (int i = 0; i < destination.Count; i++)
                    {
                        var item = destination[i] as MessageSenderUser;
                        var index = -1;

                        for (int j = 0; j < origin.Count; j++)
                        {
                            if (origin[j] == item.UserId)
                            {
                                index = j;
                                break;
                            }
                        }

                        if (index == -1)
                        {
                            destination.Remove(item);
                            i--;
                        }
                    }
                }
                else
                {
                    RecentUsers.Items.ReplaceWith(origin.Select(x => new MessageSenderUser(x)));
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
                //visual.Offset = new Vector3();
                visual.Properties.InsertVector3("Translation", Vector3.Zero);

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
            visual.StartAnimation("Translation", offset);

            batch.End();
        }

        public ICommand JoinCommand
        {
            get => JoinButton.Command;
            set => JoinButton.Command = Command = value;
        }
    }
}
