using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.TL.Help.Methods;
using Telegram.Api.TL.LangPack.Methods;
using Telegram.Api.TL.Messages.Methods;
using Telegram.Api.TL.Methods;
using Telegram.Api.TL.Phone;
using Telegram.Api.TL.Phone.Methods;
using Telegram.Logs;
using Template10.Common;
using Unigram.Common;
using Unigram.Common.Dialogs;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Core;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.Views;
using Windows.Media.Playback;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class MainViewModel : UnigramViewModelBase,
        IHandle<TLUpdatePhoneCall>,
        IHandle<TLUpdateUserTyping>,
        IHandle<TLUpdateChatUserTyping>,
        IHandle<UpdatingEventArgs>,
        IHandle<UpdateCompletedEventArgs>,
        IHandle<TLMessageCommonBase>,
        IHandle<TLUpdateReadMessagesContents>
    {
        private readonly IUpdatesService _updatesService;
        private readonly IPushService _pushService;
        private readonly IVibrationService _vibrationService;
        private readonly ILiveLocationService _liveLocationService;
        private readonly IPasscodeService _passcodeService;

        private readonly ConcurrentDictionary<int, InputTypingManager> _typingManagers;
        private readonly ConcurrentDictionary<int, InputTypingManager> _chatTypingManagers;

        public bool Refresh { get; set; }

        public MainViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IUpdatesService updatesService, IPushService pushService, IVibrationService vibrationService, ILiveLocationService liveLocationService, IContactsService contactsService, IPasscodeService passcodeService, DialogsViewModel dialogs)
            : base(protoService, cacheService, aggregator)
        {
            _updatesService = updatesService;
            _pushService = pushService;
            _vibrationService = vibrationService;
            _liveLocationService = liveLocationService;
            _passcodeService = passcodeService;

            _typingManagers = new ConcurrentDictionary<int, InputTypingManager>();
            _chatTypingManagers = new ConcurrentDictionary<int, InputTypingManager>();

            //Dialogs = new DialogCollection(protoService, cacheService);
            SearchDialogs = new ObservableCollection<TLDialog>();
            Dialogs = dialogs;
            Contacts = new ContactsViewModel(protoService, cacheService, aggregator, contactsService);
            Calls = new CallsViewModel(protoService, cacheService, aggregator);

            _selfDestructTimer = new YoloTimer(CheckSelfDestructMessages, this);
            _selfDestructItems = new List<TLMessage>();

            aggregator.Subscribe(this);

            LiveLocationCommand = new RelayCommand(LiveLocationExecute);
            StopLiveLocationCommand = new RelayCommand(StopLiveLocationExecute);
        }

        public override IDispatcherWrapper Dispatcher
        {
            get => base.Dispatcher;
            set
            {
                base.Dispatcher = value;
                Dialogs.Dispatcher = value;
                Contacts.Dispatcher = value;
                Calls.Dispatcher = value;
            }
        }

        public ILiveLocationService LiveLocation => _liveLocationService;
        public IPasscodeService Passcode => _passcodeService;

        public RelayCommand LiveLocationCommand { get; }
        private async void LiveLocationExecute()
        {
            await new LiveLocationsView().ShowQueuedAsync();
        }

        public RelayCommand StopLiveLocationCommand { get; }
        private void StopLiveLocationExecute()
        {
            _liveLocationService.StopTracking();
        }

        private YoloTimer _selfDestructTimer;
        private List<TLMessage> _selfDestructItems;

        public void Handle(TLUpdateReadMessagesContents update)
        {
            var messages = new List<TLMessage>(update.Messages.Count);
            foreach (var readMessageId in update.Messages)
            {
                var message = CacheService.GetMessage(readMessageId) as TLMessage;
                if (message != null && (message.Media is TLMessageMediaPhoto photoMedia && photoMedia.HasTTLSeconds && photoMedia.DestructDate == null) || (message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds && documentMedia.DestructDate == null))
                {
                    messages.Add(message);
                }
            }

            BeginOnUIThread(() =>
            {
                foreach (var message in messages)
                {
                    var destructIn = 0;
                    if (message.Media is TLMessageMediaPhoto photoMedia)
                    {
                        destructIn = photoMedia.TTLSeconds ?? 0;
                        photoMedia.DestructDate = DateTime.Now.AddSeconds(destructIn);
                        photoMedia.DestructIn--;
                    }
                    else if (message.Media is TLMessageMediaDocument documentMedia)
                    {
                        destructIn = documentMedia.TTLSeconds ?? 0;
                        documentMedia.DestructDate = DateTime.Now.AddSeconds(destructIn);
                        documentMedia.DestructIn--;
                    }

                    SelfDestructIn(message, destructIn);
                }
            });
        }

        void SelfDestructIn(TLMessage item, int delay)
        {
            _selfDestructItems.Add(item);

            if (!_selfDestructTimer.IsActive || _selfDestructTimer.RemainingTime.TotalSeconds > delay)
            {
                //_selfDestructTimer.CallOnce(delay);
                _selfDestructTimer.CallOnce(1);
            }
        }

        void CheckSelfDestructMessages(object state)
        {
            var now = DateTime.Now;
            var nextDestructIn = 0;
            for (int i = 0; i < _selfDestructItems.Count; i++)
            {
                var item = _selfDestructItems[i];

                var destructIn = 0;
                if (item.Media is ITLMessageMediaDestruct destructMedia)
                {
                    //if (destructMedia.DestructDate <= now)
                    //{
                    //    //destructIn = (int)((now - destructMedia.DestructDate ?? 0) / 1000);
                    //    destructIn = (int)(now - destructMedia.DestructDate.Value).TotalSeconds;
                    //}
                    //else
                    //{
                    //    destructIn = 0;
                    //}
                    if (destructMedia.DestructDate > now)
                    {
                        //destructIn = (int)((now - destructMedia.DestructDate ?? 0) / 1000);
                        destructIn = (int)Math.Ceiling((destructMedia.DestructDate.Value - now).TotalSeconds);
                    }
                    else
                    {
                        destructIn = 0;
                    }

                    BeginOnUIThread(() => destructMedia.DestructIn = Math.Max(0, destructIn - 1));
                }

                if (destructIn > 0)
                {
                    if (nextDestructIn > 0)
                    {
                        if (nextDestructIn > destructIn)
                        {
                            nextDestructIn = destructIn;
                        }
                    }
                    else
                    {
                        nextDestructIn = destructIn;
                    }
                }
                else
                {
                    PublishExpiredMessage(item);
                    _selfDestructItems.RemoveAt(i);
                    i--;
                }
            }

            if (nextDestructIn > 0)
            {
                //_selfDestructTimer.CallOnce(nextDestructIn);
                _selfDestructTimer.CallOnce(1);
            }
        }

        void PublishExpiredMessage(TLMessage message)
        {
            if (message.Media is TLMessageMediaPhoto photoMedia)
            {
                photoMedia.Photo = null;
                photoMedia.Caption = null;
                photoMedia.HasPhoto = false;
                photoMedia.HasCaption = false;

                photoMedia.DestructDate = null;
                photoMedia.DestructIn = null;
            }
            else if (message.Media is TLMessageMediaDocument documentMedia)
            {
                documentMedia.Document = null;
                documentMedia.Caption = null;
                documentMedia.HasDocument = false;
                documentMedia.HasCaption = false;

                documentMedia.DestructDate = null;
                documentMedia.DestructIn = null;
            }

            Aggregator.Publish(new MessageExpiredEventArgs(message));
        }

        public void Handle(UpdatingEventArgs e)
        {
            ProtoService.SetMessageOnTime(5, "Updating...");
        }

        public void Handle(UpdateCompletedEventArgs e)
        {
            ProtoService.SetMessageOnTime(0, null);
        }

        #region Typing

        public void Handle(TLUpdateUserTyping update)
        {
            var user = CacheService.GetUser(update.UserId) as TLUser;
            if (user == null)
            {
                return;
            }

            if (user.IsSelf)
            {
                return;
            }

            var dialog = CacheService.GetDialog(user.ToPeer());
            if (dialog == null)
            {
                return;
            }

            _typingManagers.TryGetValue(update.UserId, out InputTypingManager typingManager);
            if (typingManager == null)
            {
                typingManager = new InputTypingManager(users =>
                {
                    dialog.TypingSubtitle = DialogViewModel.GetTypingString(user.ToPeer(), users, CacheService.GetUser, null);
                    dialog.IsTyping = true;
                },
                () =>
                {
                    dialog.TypingSubtitle = null;
                    dialog.IsTyping = false;
                });

                _typingManagers[update.UserId] = typingManager;
            }

            var action = update.Action;
            if (action is TLSendMessageCancelAction)
            {
                typingManager.RemoveTypingUser(update.UserId);
                return;
            }

            typingManager.AddTypingUser(update.UserId, action);
        }

        public void Handle(TLUpdateChatUserTyping update)
        {
            var chat = CacheService.GetChat(update.ChatId) as TLChatBase;
            if (chat == null)
            {
                return;
            }

            var dialog = CacheService.GetDialog(chat.ToPeer());
            if (dialog == null)
            {
                return;
            }

            _typingManagers.TryGetValue(update.ChatId, out InputTypingManager typingManager);
            if (typingManager == null)
            {
                typingManager = new InputTypingManager(users =>
                {
                    dialog.TypingSubtitle = DialogViewModel.GetTypingString(chat.ToPeer(), users, CacheService.GetUser, null);
                    dialog.IsTyping = true;
                },
                () =>
                {
                    dialog.TypingSubtitle = null;
                    dialog.IsTyping = false;
                });

                _typingManagers[update.ChatId] = typingManager;
            }

            var action = update.Action;
            if (action is TLSendMessageCancelAction)
            {
                typingManager.RemoveTypingUser(update.UserId);
                return;
            }

            typingManager.AddTypingUser(update.UserId, action);
        }

        #endregion

        public TLVector<TLTopPeerCategoryPeers> TopPeers { get; private set; }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New)
            {
                Execute.BeginOnThreadPool(() => _pushService.RegisterAsync());
            }

            BeginOnUIThread(() => Calls.OnNavigatedToAsync(parameter, mode, state));
            //Dispatch(() => Dialogs.LoadFirstSlice());
            //Dispatch(() => Contacts.getTLContacts());
            //Dispatch(() => Contacts.GetSelfAsync());

            //ProtoService.SendRequestAsync<object>("langpack.getStrings", new TLLangPackGetStrings { Keys = new TLVector<string> { "CHANNEL_MESSAGE_GEOLIVE", "CHAT_MESSAGE_GEOLIVE", "MESSAGE_GEOLIVE", "PINNED_GEOLIVE" }, LangCode = "it" }, result =>
            //{
            //    Debugger.Break();
            //},
            //fault =>
            //{
            //    Debugger.Break();
            //});

            //ProtoService.SendRequestAsync<TLUpdatesBase>("help.getAppChangelog", new TLHelpGetAppChangelog { PrevAppVersion = "4.6" }, result =>
            //{
            //    _updatesService.ProcessUpdates(result, true);
            //},
            //fault =>
            //{
            //    Debugger.Break();
            //});

            //var obj = new TLLangPackGetStrings { LangCode = "fa", Keys = new TLVector<string> { "AUTH_REGION", "CHANNEL_MESSAGE_AUDIO", "CHANNEL_MESSAGE_CONTACT", "CHANNEL_MESSAGE_DOC", "CHANNEL_MESSAGE_GAME", "CHANNEL_MESSAGE_GEO", "CHANNEL_MESSAGE_GEOLIVE", "CHANNEL_MESSAGE_GIF", "CHANNEL_MESSAGE_NOTEXT", "CHANNEL_MESSAGE_PHOTO", "CHANNEL_MESSAGE_ROUND", "CHANNEL_MESSAGE_STICKER", "CHANNEL_MESSAGE_TEXT", "CHANNEL_MESSAGE_VIDEO", "CHAT_ADD_MEMBER", "CHAT_ADD_YOU", "CHAT_CREATED", "CHAT_DELETE_MEMBER", "CHAT_DELETE_YOU", "CHAT_LEFT", "CHAT_MESSAGE_AUDIO", "CHAT_MESSAGE_CONTACT", "CHAT_MESSAGE_DOC", "CHAT_MESSAGE_FWDS", "CHAT_MESSAGE_GAME", "CHAT_MESSAGE_GEO", "CHAT_MESSAGE_GEOLIVE", "CHAT_MESSAGE_GIF", "CHAT_MESSAGE_INVOICE", "CHAT_MESSAGE_NOTEXT", "CHAT_MESSAGE_PHOTO", "CHAT_MESSAGE_ROUND", "CHAT_MESSAGE_STICKER", "CHAT_MESSAGE_TEXT", "CHAT_MESSAGE_VIDEO", "CHAT_PHOTO_EDITED", "CHAT_RETURNED", "CHAT_TITLE_EDITED", "CONTACT_JOINED", "ENCRYPTED_MESSAGE", "ENCRYPTION_ACCEPT", "ENCRYPTION_REQUEST", "LOCKED_MESSAGE", "MESSAGE_AUDIO", "MESSAGE_CONTACT", "MESSAGE_DOC", "MESSAGE_FWDS", "MESSAGE_GAME", "MESSAGE_GEO", "MESSAGE_GEOLIVE", "MESSAGE_GIF", "MESSAGE_INVOICE", "MESSAGE_NOTEXT", "MESSAGE_PHOTO", "MESSAGE_PHOTO_SECRET", "MESSAGE_ROUND", "MESSAGE_SCREENSHOT", "MESSAGE_STICKER", "MESSAGE_TEXT", "MESSAGE_VIDEO", "MESSAGE_VIDEO_SECRET", "PHONE_CALL_MISSED", "PHONE_CALL_REQUEST", "PINNED_AUDIO", "PINNED_CONTACT", "PINNED_DOC", "PINNED_GAME", "PINNED_GEO", "PINNED_GEOLIVE", "PINNED_GIF", "PINNED_INVOICE", "PINNED_NOTEXT", "PINNED_PHOTO", "PINNED_ROUND", "PINNED_STICKER", "PINNED_TEXT", "PINNED_VIDEO" } };

            //const string caption = "langpack.getStrings";
            //ProtoService.SendRequestAsync<TLVector<TLLangPackStringBase>>(caption, obj, result =>
            //{
            //    var builder = new StringBuilder();

            //    foreach (var item in result.OfType<TLLangPackString>())
            //    {
            //        var value = item.Value;
            //        if (item.Key.StartsWith("CHANNEL") || item.Key.StartsWith("MESSAGE") || item.Key.StartsWith("PINNED"))
            //        {
            //            value = value.TrimStart("%1$@").TrimStart(':', ' ');
            //        }
            //        else if (item.Key.StartsWith("CHAT"))
            //        {
            //            value = value.Replace(" %2$@", string.Empty).Replace("@%2$@", string.Empty).Replace("%2$ ", string.Empty).Replace("«%2$@»", string.Empty).TrimEnd();
            //        }

            //        value = value.Replace("%1$@", "{0}");
            //        value = value.Replace("%2$@", "{1}");
            //        value = value.Replace("%3$@", "{2}");

            //        builder.AppendLine($"{item.Key}\t{value}");
            //    }

            //    var yella = builder.ToString();
            //    var cips = true;
            //},
            //fault =>
            //{
            //    Debugger.Break();
            //});

            if (Refresh)
            {
                Refresh = false;
                Dialogs.Items.Clear();
                Execute.BeginOnThreadPool(() => Dialogs.LoadFirstSlice());
            }

            //ProtoService.GetTopPeersAsync(TLContactsGetTopPeers.Flag.BotsInline, 0, 0, 0, result =>
            //{
            //    var topPeers = result as TLContactsTopPeers;
            //    if (topPeers != null)
            //    {
            //        TopPeers = topPeers.Categories;
            //    }
            //});

            return Task.CompletedTask;
        }

        public void Handle(TLMessageCommonBase commonMessage)
        {
            BeginOnUIThread(() => Notify(commonMessage));
        }

        public void Notify(TLMessageCommonBase commonMessage)
        {
            //if (this._stateService.SuppressNotifications)
            //{
            //    return;
            //}
            if (commonMessage.IsOut)
            {
                return;
            }

            if (!commonMessage.IsUnread)
            {
                return;
            }

            if (commonMessage is TLMessage message && message.IsSilent)
            {
                return;
            }

            TLUser from = null;
            if (commonMessage.FromId != null && commonMessage.FromId.Value >= 0)
            {
                from = CacheService.GetUser(commonMessage.FromId) as TLUser;
                if (from == null)
                {
                    return;
                }
            }

            try
            {
                TLObject activeDialog = CheckActiveDialog();
                TLPeerBase toId = commonMessage.ToId;
                var fromId = commonMessage.FromId;
                var suppress = false;
                TLDialog dialog = null;
                if (toId is TLPeerChat && activeDialog is TLChat && toId.Id == ((TLChat)activeDialog).Id)
                {
                    suppress = App.IsActive && App.IsVisible;
                }
                if (toId is TLPeerChannel && activeDialog is TLChannel && toId.Id == ((TLChannel)activeDialog).Id)
                {
                    suppress = App.IsActive && App.IsVisible;
                }
                else if (toId is TLPeerUser && activeDialog is TLUserBase && ((from != null && from.IsSelf) || fromId.Value == ((TLUserBase)activeDialog).Id))
                {
                    suppress = App.IsActive && App.IsVisible;
                }

                if (!suppress)
                {
                    TLChatBase chat = null;
                    TLUser user = null;
                    TLChannel channel = null;
                    if (commonMessage.ToId is TLPeerChat)
                    {
                        chat = CacheService.GetChat(commonMessage.ToId.Id);
                        dialog = CacheService.GetDialog(new TLPeerChat { Id = commonMessage.ToId.Id });
                    }
                    else if (commonMessage.ToId is TLPeerChannel)
                    {
                        chat = CacheService.GetChat(commonMessage.ToId.Id);
                        channel = (chat as TLChannel);
                        dialog = CacheService.GetDialog(new TLPeerChannel { ChannelId = commonMessage.ToId.Id });
                    }
                    else if (commonMessage.IsOut)
                    {
                        user = CacheService.GetUser(commonMessage.ToId.Id) as TLUser;
                        dialog = CacheService.GetDialog(new TLPeerUser { UserId = commonMessage.ToId.Id });
                    }
                    else
                    {
                        user = CacheService.GetUser(commonMessage.FromId) as TLUser;
                        dialog = CacheService.GetDialog(new TLPeerUser { UserId = commonMessage.FromId.Value });
                    }

                    var now = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                    if (chat != null)
                    {
                        var notifySettingsBase = CacheService.GetFullChat(chat.Id)?.NotifySettings;
                        if (notifySettingsBase == null)
                        {
                            notifySettingsBase = ((dialog != null) ? dialog.NotifySettings : null);
                        }

                        if (notifySettingsBase == null)
                        {
                            if (channel != null)
                            {
                                ProtoService.GetFullChannelAsync(channel.ToInputChannel(), chatFull =>
                                {
                                    //chat.NotifySettings = chatFull.FullChat.NotifySettings;
                                    if (dialog != null)
                                    {
                                        dialog.NotifySettings = chatFull.FullChat.NotifySettings;

                                        BeginOnUIThread(() =>
                                        {
                                            dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                                            dialog.RaisePropertyChanged(() => dialog.Self);
                                        });
                                    }
                                }, null);
                            }
                            else
                            {
                                ProtoService.GetFullChatAsync(chat.Id, chatFull =>
                                {
                                    //chat.NotifySettings = chatFull.FullChat.NotifySettings;
                                    if (dialog != null)
                                    {
                                        dialog.NotifySettings = chatFull.FullChat.NotifySettings;

                                        BeginOnUIThread(() =>
                                        {
                                            dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                                            dialog.RaisePropertyChanged(() => dialog.Self);
                                        });
                                    }
                                }, null);
                            }
                        }

                        var notifySettings = notifySettingsBase as TLPeerNotifySettings;
                        suppress = (notifySettings == null || notifySettings.MuteUntil > now) && !commonMessage.IsMentioned;
                    }

                    if (user != null)
                    {
                        var notifySettingsBase = CacheService.GetFullUser(user.Id)?.NotifySettings;
                        if (notifySettingsBase == null)
                        {
                            notifySettingsBase = ((dialog != null) ? dialog.NotifySettings : null);
                        }

                        if (notifySettingsBase == null)
                        {
                            ProtoService.GetFullUserAsync(user.ToInputUser(), userFull =>
                            {
                                //user.NotifySettings = userFull.NotifySettings;
                                if (dialog != null)
                                {
                                    dialog.NotifySettings = userFull.NotifySettings;

                                    BeginOnUIThread(() =>
                                    {
                                        dialog.RaisePropertyChanged(() => dialog.NotifySettings);
                                        dialog.RaisePropertyChanged(() => dialog.Self);
                                    });
                                }
                            }, null);
                        }

                        var notifySettings = notifySettingsBase as TLPeerNotifySettings;
                        suppress = (notifySettings == null || notifySettings.MuteUntil > now || user.IsSelf);
                    }

                    if (!suppress)
                    {
                        if (dialog != null)
                        {
                            suppress = CheckLastNotificationTime(dialog, now, commonMessage.IsMentioned);
                        }

                        if (!suppress)
                        {
                            if (ApplicationSettings.Current.InAppPreview)
                            {
                                _pushService.Notify(commonMessage);
                            }

                            if (_lastNotificationTime.HasValue)
                            {
                                var totalSeconds = (DateTime.Now - _lastNotificationTime.Value).TotalSeconds;
                                if (totalSeconds > 0.0 && totalSeconds < 2.0)
                                {
                                    suppress = true;
                                }
                            }

                            _lastNotificationTime = DateTime.Now;

                            if (suppress)
                            {
                                Log.Write(string.Format("Cancel notification reason=[lastNotificationTime] msg_id={0} last_notification_time={1}, now={2}", commonMessage.Id, _lastNotificationTime, DateTime.Now), null);
                            }
                            else
                            {
                                if (ApplicationSettings.Current.InAppVibrate)
                                {
                                    _vibrationService.VibrateAsync();
                                }

                                if (ApplicationSettings.Current.InAppSounds)
                                {
                                    //if (_notificationPlayer == null)
                                    //{
                                    //    _notificationPlayer = new MediaPlayer();
                                    //    _notificationPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///Assets/Sounds/Default.wav"));
                                    //}

                                    //_notificationPlayer.Pause();
                                    //_notificationPlayer.PlaybackSession.Position = TimeSpan.Zero;
                                    //_notificationPlayer.Play();



                                    //string text = "Sounds/Default.wav";
                                    //if (toId is TLPeerChat && !string.IsNullOrEmpty(s.GroupSound))
                                    //{
                                    //    text = "Sounds/" + s.GroupSound + ".wav";
                                    //}
                                    //else if (!string.IsNullOrEmpty(s.ContactSound))
                                    //{
                                    //    text = "Sounds/" + s.ContactSound + ".wav";
                                    //}
                                    //if (toId is TLPeerChat && chat != null && chat.NotifySettings is TLPeerNotifySettings)
                                    //{
                                    //    text = "Sounds/" + ((TLPeerNotifySettings)chat.NotifySettings).Sound.Value + ".wav";
                                    //}
                                    //else if (toId is TLPeerUser && user != null && user.NotifySettings is TLPeerNotifySettings)
                                    //{
                                    //    text = "Sounds/" + ((TLPeerNotifySettings)user.NotifySettings).Sound.Value + ".wav";
                                    //}
                                    //if (!Utils.XapContentFileExists(text))
                                    //{
                                    //    text = "Sounds/Default.wav";
                                    //}
                                    //System.IO.Stream stream = TitleContainer.OpenStream(text);
                                    //SoundEffect soundEffect = SoundEffect.FromStream(stream);
                                    //FrameworkDispatcher.Update();
                                    //soundEffect.Play();
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                TLUtils.WriteLine(ex.ToString(), LogSeverity.Error);
            }
        }

        private MediaPlayer _notificationPlayer;

        private DateTime? _lastNotificationTime;
        private Context<DateTime?> _lastNotificationTimes = new Context<DateTime?>();
        private Context<int> _unmutedCounts = new Context<int>();

        private TLObject CheckActiveDialog()
        {
            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service == null)
            {
                return null;
            }

            if (service.Frame.Content is DialogPage page && service.CurrentPageParam is TLPeerBase peer)
            {
                if (peer is TLPeerUser peerUser)
                {
                    return CacheService.GetUser(peerUser.UserId);
                }
                else if (peer is TLPeerChat peerChat)
                {
                    return CacheService.GetChat(peerChat.ChatId);
                }
                else if (peer is TLPeerChannel peerChannel)
                {
                    return CacheService.GetChat(peerChannel.ChannelId);
                }
            }

            return null;
        }

        private bool CheckLastNotificationTime(TLDialog dialog, int now, bool mention)
        {
            if (dialog == null)
            {
                return false;
            }

            var notifySettings = dialog.NotifySettings as TLPeerNotifySettings;
            if (notifySettings != null && notifySettings.MuteUntil > now && !mention)
            {
                _lastNotificationTimes[dialog.Id] = null;
                _unmutedCounts[dialog.Id] = 0;
                return true;
            }

            if (!_lastNotificationTimes[dialog.Id].HasValue)
            {
                _lastNotificationTimes[dialog.Id] = DateTime.Now;
                _unmutedCounts[dialog.Id] = 1;
                return false;
            }

            var totalSeconds = (DateTime.Now - _lastNotificationTimes[dialog.Id].Value).TotalSeconds;
            if (totalSeconds > 15.0)
            {
                _lastNotificationTimes[dialog.Id] = DateTime.Now;
                _unmutedCounts[dialog.Id] = 1;
                return false;
            }

            var unmutedCount = _unmutedCounts[dialog.Id];
            if (unmutedCount < 1)
            {
                _unmutedCounts[dialog.Id]++;
                return false;
            }

            _unmutedCounts[dialog.Id]++;
            return true;
        }

        private byte[] secretP;
        private byte[] a_or_b;

        public async void Handle(TLUpdatePhoneCall update)
        {
            await VoIPConnection.Current.SendUpdateAsync(update);
            await Task.Delay(2000);

            //if (update.PhoneCall is TLPhoneCallDiscarded discarded)
            //{
            //    if (discarded.IsNeedRating)
            //    {
            //        Debugger.Break();
            //    }

            //    if (discarded.IsNeedDebug)
            //    {
            //        Debugger.Break();
            //    }
            //}

            return;

            if (update.PhoneCall is TLPhoneCallRequested callRequested)
            {
                var reqReceived = new TLPhoneReceivedCall();
                reqReceived.Peer = new TLInputPhoneCall();
                reqReceived.Peer.Id = callRequested.Id;
                reqReceived.Peer.AccessHash = callRequested.AccessHash;

                ProtoService.SendRequestAsync<bool>("phone.receivedCall", reqReceived, null, null);

                var user = CacheService.GetUser(callRequested.AdminId) as TLUser;

                BeginOnUIThread(async () =>
                {
                    var dialog = await TLMessageDialog.ShowAsync(user.DisplayName, "CAAAALLL", "OK", "Cancel");
                    if (dialog == Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
                    {
                        var config = await ProtoService.GetDHConfigAsync(0, 256);
                        if (config.IsSucceeded)
                        {
                            var dh = config.Result;
                            if (!TLUtils.CheckPrime(dh.P, dh.G))
                            {
                                return;
                            }

                            secretP = dh.P;

                            var salt = new byte[256];
                            var secureRandom = new SecureRandom();
                            secureRandom.NextBytes(salt);

                            a_or_b = salt;

                            var g_b = MTProtoService.GetGB(salt, dh.G, dh.P);

                            var request = new TLPhoneAcceptCall
                            {
                                GB = g_b,
                                Peer = new TLInputPhoneCall
                                {
                                    Id = callRequested.Id,
                                    AccessHash = callRequested.AccessHash
                                },
                                Protocol = new TLPhoneCallProtocol
                                {
                                    IsUdpP2p = true,
                                    IsUdpReflector = true,
                                    MinLayer = 65,
                                    MaxLayer = 65,
                                }
                            };

                            var response = await ProtoService.SendRequestAsync<TLPhonePhoneCall>("phone.acceptCall", request);
                            if (response.IsSucceeded)
                            {
                            }
                        }
                    }
                    else
                    {
                        var req = new TLPhoneDiscardCall();
                        req.Peer = new TLInputPhoneCall();
                        req.Peer.Id = callRequested.Id;
                        req.Peer.AccessHash = callRequested.AccessHash;
                        req.Reason = new TLPhoneCallDiscardReasonHangup();

                        ProtoService.SendRequestAsync<TLPhonePhoneCall>("phone.acceptCall", req, null, null);
                    }
                });
            }
            else if (update.PhoneCall is TLPhoneCall call)
            {
                var auth_key = computeAuthKey(call);
                var g_a = call.GAOrB;

                var buffer = TLUtils.Combine(auth_key, g_a);
                var sha256 = Utils.ComputeSHA256(buffer);

                var emoji = EncryptionKeyEmojifier.EmojifyForCall(sha256);

                var user = CacheService.GetUser(call.AdminId) as TLUser;

                BeginOnUIThread(async () =>
                {
                    var dialog = await TLMessageDialog.ShowAsync(user.DisplayName, string.Join(" ", emoji), "OK");
                });
            }
        }

        private byte[] computeAuthKey(TLPhoneCall call)
        {
            BigInteger g_a = new BigInteger(1, call.GAOrB);
            BigInteger p = new BigInteger(1, secretP);

            g_a = g_a.ModPow(new BigInteger(1, a_or_b), p);

            byte[] authKey = g_a.ToByteArray();
            if (authKey.Length > 256)
            {
                byte[] correctedAuth = new byte[256];
                Buffer.BlockCopy(authKey, authKey.Length - 256, correctedAuth, 0, 256);
                authKey = correctedAuth;
            }
            else if (authKey.Length < 256)
            {
                byte[] correctedAuth = new byte[256];
                Buffer.BlockCopy(authKey, 0, correctedAuth, 256 - authKey.Length, authKey.Length);
                for (int a = 0; a < 256 - authKey.Length; a++)
                {
                    authKey[a] = 0;
                }
                authKey = correctedAuth;
            }
            byte[] authKeyHash = Utils.ComputeSHA1(authKey);
            byte[] authKeyId = new byte[8];
            Buffer.BlockCopy(authKeyHash, authKeyHash.Length - 8, authKeyId, 0, 8);

            return authKey;
        }

        //END OF EXPERIMENTS
        //public DialogCollection Dialogs { get; private set; }

        public ObservableCollection<TLDialog> SearchDialogs { get; private set; }

        public DialogsViewModel Dialogs { get; private set; }

        public ContactsViewModel Contacts { get; private set; }

        public CallsViewModel Calls { get; private set; }
    }

    public class YoloTimer
    {
        private Timer _timer;
        private TimerCallback _callback;
        private DateTime? _start;

        public YoloTimer(TimerCallback callback, object state)
        {
            _callback = callback;
            _timer = new Timer(OnCallback, state, Timeout.Infinite, Timeout.Infinite);
        }

        private void OnCallback(object state)
        {
            _start = null;
            _callback(state);
        }

        public void CallOnce(int seconds)
        {
            _start = DateTime.Now;
            _timer.Change(seconds * 1000, Timeout.Infinite);
        }

        public bool IsActive
        {
            get
            {
                return _start.HasValue;
            }
        }

        public TimeSpan RemainingTime
        {
            get
            {
                if (_start.HasValue)
                {
                    return DateTime.Now - _start.Value;
                }

                return TimeSpan.Zero;
            }
        }
    }
}
