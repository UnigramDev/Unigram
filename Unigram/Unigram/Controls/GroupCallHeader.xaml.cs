using System;
using System.Collections.Generic;
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
    public sealed partial class GroupCallHeader : HyperlinkButton
    {
        private UIElement _parent;
        private int _prevId;

        public GroupCallHeader()
        {
            InitializeComponent();
        }

        public void InitializeParent(UIElement parent, IProtoService protoService)
        {
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

        public bool UpdateGroupCall(Chat chat, GroupCall call)
        {
            var visible = true;

            if (chat.VoiceChat.GroupCallId != call?.Id || call == null || call.IsJoined || call.IsActive)
            {
                ShowHide(false);
                visible = false;
            }
            else
            {
                ShowHide(true);

                TitleLabel.Text = Strings.Resources.VoipGroupVoiceChat;
                ServiceLabel.Text = call.ParticipantCount > 0 ? Locale.Declension("Participants", call.ParticipantCount) : Strings.Resources.MembersTalkingNobody;

                var destination = RecentUsers.Items;
                var origin = call.RecentSpeakers;

                if (_prevId == call.Id)
                {
                    for (int i = 0; i < origin.Count; i++)
                    {
                        var item = origin[i];
                        var index = -1;

                        for (int j = 0; j < destination.Count; j++)
                        {
                            if (destination[j].IsEqual(item.Speaker))
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
                            destination.Insert(Math.Min(i, destination.Count), item.Speaker);
                        }
                    }

                    for (int i = 0; i < destination.Count; i++)
                    {
                        var item = destination[i];
                        var index = -1;

                        for (int j = 0; j < origin.Count; j++)
                        {
                            if (origin[j].Speaker.IsEqual(item))
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
                    RecentUsers.Items.ReplaceWith(origin.Select(x => x.Speaker));
                }
            }

            _prevId = call?.Id ?? 0;
            return visible;
        }

        public void UpdateChatActions(IDictionary<int, ChatAction> actions)
        {
            if (actions != null && actions.Count > 0)
            {
                //MessageLabel.Text = InputChatActionManager.GetSpeakingString(null, actions);
            }
            else
            {
                MessageLabel.Text = string.Empty;
            }
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
