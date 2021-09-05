using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Input;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public sealed partial class GroupCallHeader : HyperlinkButton
    {
        private readonly DispatcherTimer _scheduledTimer;

        private UIElement _parent;
        private GroupCall _call;

        public GroupCallHeader()
        {
            InitializeComponent();

            _scheduledTimer = new DispatcherTimer();
            _scheduledTimer.Tick += OnTick;
            _scheduledTimer.Interval = TimeSpan.FromSeconds(1);
        }

        private void OnTick(object sender, object e)
        {
            if (_call != null && _call.ScheduledStartDate != 0)
            {
                JoinButton.Content = _call.GetStartsIn();
            }
            else
            {
                _scheduledTimer.Stop();
            }
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
            var channel = chat.Type is ChatTypeSupergroup super && super.IsChannel;

            //if (chat.VoiceChat.GroupCallId != call?.Id || !chat.VoiceChat.HasParticipants || call == null || call.IsJoined)
            //{
            //    ShowHide(false);
            //    visible = false;
            //}
            //else
            if (call != null && chat.VoiceChat.GroupCallId == call.Id && ((chat.VoiceChat.HasParticipants && !(call.IsJoined || call.NeedRejoin)) || call.ScheduledStartDate > 0))
            {
                ShowHide(true);

                TitleLabel.Text = call.ScheduledStartDate > 0 && call.Title.Length > 0 ? call.Title : channel ? Strings.Resources.VoipChannelVoiceChat : Strings.Resources.VoipGroupVoiceChat;
                ServiceLabel.Text = call.ParticipantCount > 0 ? Locale.Declension("Participants", call.ParticipantCount) : Strings.Resources.MembersTalkingNobody;

                if (call.ScheduledStartDate != 0)
                {
                    var date = Converters.Converter.DateTime(call.ScheduledStartDate);
                    var duration = date - DateTime.Now;

                    if (duration.TotalDays < 1)
                    {
                        _scheduledTimer.Start();
                    }
                    else
                    {
                        _scheduledTimer.Stop();
                    }

                    TitleLabel.Text = call.Title.Length > 0 ? call.Title : channel ? Strings.Resources.VoipChannelScheduledVoiceChat : Strings.Resources.VoipGroupScheduledVoiceChat;

                    JoinButton.Background = BootStrapper.Current.Resources["VoiceChatPurpleBrush"] as Brush;
                    JoinButton.Content = call.GetStartsIn();
                }
                else
                {
                    _scheduledTimer.Stop();

                    TitleLabel.Text = channel ? Strings.Resources.VoipChannelVoiceChat : Strings.Resources.VoipGroupVoiceChat;

                    JoinButton.Background = BootStrapper.Current.Resources["StartButtonBackground"] as Brush;
                    JoinButton.Content = Strings.Resources.VoipChatJoin;
                }

                var destination = RecentUsers.Items;
                var origin = call.RecentSpeakers;

                if (_call?.Id == call.Id)
                {
                    for (int i = 0; i < origin.Count; i++)
                    {
                        var item = origin[i];
                        var index = -1;

                        for (int j = 0; j < destination.Count; j++)
                        {
                            if (destination[j].IsEqual(item.ParticipantId))
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
                            destination.Insert(Math.Min(i, destination.Count), item.ParticipantId);
                        }
                    }

                    for (int i = 0; i < destination.Count; i++)
                    {
                        var item = destination[i];
                        var index = -1;

                        for (int j = 0; j < origin.Count; j++)
                        {
                            if (origin[j].ParticipantId.IsEqual(item))
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
                    RecentUsers.Items.ReplaceWith(origin.Select(x => x.ParticipantId));
                }
            }
            else
            {
                ShowHide(false);
                visible = false;
            }

            _call = call;
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
