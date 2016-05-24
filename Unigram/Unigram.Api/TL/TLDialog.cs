using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Telegram.Api.Helpers;
#if WIN_RT
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
#elif WINDOWS_PHONE
using System.Windows;
using System.Windows.Media;
#endif
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLDialogBase : TLObject
    {

        public object MessagesSyncRoot = new object();

        public int Index
        {
            get { return Peer != null && Peer.Id != null? Peer.Id.Value : default(int); }
            set
            {
                //NOTE: No need to set Index during deserialization. Possible null reference
            }
        }

        public TLPeerBase Peer { get; set; }

        public TLInt TopMessageId { get; set; }

        private TLInt _unreadCount;

        public TLInt UnreadCount
        {
            get { return _unreadCount; }
            set
            {
                _unreadCount = value; 
                
            }
        }

        #region Additional

        public string TypingString { get; set; }

        public TLDialogBase Self{ get { return this; } }

        /// <summary>
        /// If top message is sending message, than it has RandomId instead of Id
        /// </summary>
        public TLLong TopMessageRandomId { get; set; }

        public TLLong TopDecryptedMessageRandomId { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public TLObject _with;

        public TLObject With
        {
            get { return _with; }
            set { SetField(ref _with, value, () => With); }
        }

        public int WithId
        {
            get
            {
                if (With is TLChatBase)
                {
                    return ((TLChatBase)With).Index;
                }
                if (With is TLUserBase)
                {
                    return ((TLUserBase)With).Index;
                }
                return -1;
            }
        }

        public Visibility ChatIconVisibility
        {
            get { return Peer is TLPeerChat ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility ChatVisibility
        {
            get { return Peer is TLPeerChat || Peer is TLPeerEncryptedChat || Peer is TLPeerBroadcast ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility UserVisibility
        {
            get { return Peer is TLPeerUser || _with is TLChatForbidden || _with is TLChatEmpty ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility BotVisibility
        {
            get
            {
                var user = _with as TLUser;
                return user != null && user.IsBot ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility EncryptedChatVisibility
        {
            get { return Peer is TLPeerEncryptedChat ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility VerifiedChannelVisibility
        {
            get
            {
                var channel = With as TLChannel;
                return channel != null && channel.IsVerified ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Uri EncryptedImageSource
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                return !isLightTheme ?
                    new Uri("/Images/Dialogs/secretchat-white-WXGA.png", UriKind.Relative) :
                    new Uri("/Images/Dialogs/secretchat-black-WXGA.png", UriKind.Relative);
            }
        }

        public Brush MuteIconBackground
        {
            get
            {
                var isLightTheme = (Visibility)Application.Current.Resources["PhoneLightThemeVisibility"] == Visibility.Visible;

                return !isLightTheme
                    ? new SolidColorBrush(Color.FromArgb(255, 102, 102, 102))
                    : new SolidColorBrush(Color.FromArgb(255, 194, 194, 194));
            }
        }

        public bool IsChat
        {
            get { return Peer is TLPeerChat; }
        }

        public bool IsEncryptedChat
        {
            get { return Peer is TLPeerEncryptedChat; }
        }

        public DateTime? LastNotificationTime { get; set; }

        public int UnmutedCount { get; set; }
        #endregion

        public abstract int GetDateIndex();
        public abstract int CountMessages();
    }

    public class TLEncryptedDialog : TLDialogBase
    {
        public const uint Signature = TLConstructors.TLDialogSecret;

        #region Additional

        public TLDecryptedMessageBase _topMessage;

        public TLDecryptedMessageBase TopMessage
        {
            get
            {
                if (TLUtils.IsDisplayedDecryptedMessage(_topMessage, true))
                {
                    return _topMessage;
                }

                if (Messages != null)
                {
                    for (var i = 0; i < Messages.Count; i++)
                    {
                        if (TLUtils.IsDisplayedDecryptedMessage(Messages[i], true))
                        {
                            return Messages[i];
                        }
                    } 
                }

                return null;
            }
            set { SetField(ref _topMessage, value, () => TopMessage); }
        }

        public ObservableCollection<TLDecryptedMessageBase> Messages { get; set; }
        #endregion

        public override int GetDateIndex()
        {
            return _topMessage != null ? _topMessage.DateIndex : 0;
        }

        public override int CountMessages()
        {
            return Messages.Count;
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLPeerBase>(input);
            var topDecryptedMessageRandomId = GetObject<TLLong>(input);
            if (topDecryptedMessageRandomId.Value != 0)
            {
                TopDecryptedMessageRandomId = topDecryptedMessageRandomId;
            }

            UnreadCount = GetObject<TLInt>(input);

            _with = GetObject<TLObject>(input);
            if (_with is TLNull) { _with = null; }

            var messages = GetObject<TLVector<TLDecryptedMessageBase>>(input);
            Messages = messages != null ? 
                new ObservableCollection<TLDecryptedMessageBase>(messages.Items) : 
                new ObservableCollection<TLDecryptedMessageBase>();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);

            TopDecryptedMessageRandomId = TopDecryptedMessageRandomId ?? new TLLong(0);
            TopDecryptedMessageRandomId.ToStream(output);

            output.Write(UnreadCount.ToBytes());

            With.NullableToStream(output);

            if (Messages != null)
            {
                var messages = new TLVector<TLDecryptedMessageBase> { Items = Messages };
                messages.ToStream(output);
            }
            else
            {
                var messages = new TLVector<TLDecryptedMessageBase>();
                messages.ToStream(output);
            }
        }

        public override string ToString()
        {
            return string.Format("Index {5}, Peer {0}, IsChat {1}, UnreadCount {2}, TopMsgRandomId {3}, TopMessage {4}", With ?? Peer,
                Peer is TLPeerChat, UnreadCount, TopMessageId, TopMessage, Index);
        }

        public static int InsertMessageInOrder(IList<TLDecryptedMessageBase> messages, TLDecryptedMessageBase message)
        {
            var position = -1;

            if (messages.Count == 0)
            {
                position = 0;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].DateIndex < message.DateIndex)
                {
                    position = i;
                    break;
                }
            }

            if (position != -1)
            {
                messages.Insert(position, message);
            }

            return position;
        }
    }

    public class TLBroadcastDialog : TLDialogBase
    {
        public const uint Signature = TLConstructors.TLBroadcastDialog;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLPeerBase>(bytes, ref position);
            TopMessageId = GetObject<TLInt>(bytes, ref position);
            UnreadCount = GetObject<TLInt>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLPeerBase>(input);
            var topMessageId = GetObject<TLInt>(input);
            if (topMessageId.Value != 0)
            {
                TopMessageId = topMessageId;
            }

            UnreadCount = GetObject<TLInt>(input);

            var notifySettingsObject = GetObject<TLObject>(input);
            NotifySettings = notifySettingsObject as TLPeerNotifySettingsBase;

            var topMessageRandomId = GetObject<TLLong>(input);
            if (topMessageRandomId.Value != 0)
            {
                TopMessageRandomId = topMessageRandomId;
            }

            _with = GetObject<TLObject>(input);
            if (_with is TLNull) { _with = null; }

            var messages = GetObject<TLVector<TLMessageBase>>(input);
            Messages = messages != null ? new ObservableCollection<TLMessageBase>(messages.Items) : new ObservableCollection<TLMessageBase>();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);

            TopMessageId = TopMessageId ?? new TLInt(0);
            TopMessageId.ToStream(output);

            output.Write(UnreadCount.ToBytes());

            NotifySettings.NullableToStream(output);

            TopMessageRandomId = TopMessageRandomId ?? new TLLong(0);
            TopMessageRandomId.ToStream(output);

            With.NullableToStream(output);
            if (Messages != null)
            {
                var messages = new TLVector<TLMessageBase> { Items = Messages };
                messages.ToStream(output);
            }
            else
            {
                var messages = new TLVector<TLMessageBase>();
                messages.ToStream(output);
            }
        }

        #region Additional

        public TLMessageBase _topMessage;

        public TLMessageBase TopMessage
        {
            get { return _topMessage; }
            set { SetField(ref _topMessage, value, () => TopMessage); }
        }

        public ObservableCollection<TLMessageBase> Messages { get; set; }

        public bool ShowFrom
        {
            get { return Peer is TLPeerChat && !(TopMessage is TLMessageService); }
        }

        #endregion

        public override int GetDateIndex()
        {
            return _topMessage != null ? _topMessage.DateIndex : 0;
        }

        public override int CountMessages()
        {
            return Messages.Count;
        }

        public override string ToString()
        {
            return string.Format("Index {5}, Peer {0}, IsChat {1}, UnreadCount {2}, TopMsgId {3}, TopMessage {4}", With ?? Peer,
                Peer is TLPeerChat, UnreadCount, TopMessageId, TopMessage, Index);
        }

        public static int InsertMessageInOrder(IList<TLMessageBase> messages, TLMessageBase message)
        {
            var position = -1;

            if (messages.Count == 0)
            {
                position = 0;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].Index == 0)
                {
                    if (messages[i].DateIndex < message.DateIndex)
                    {
                        position = i;
                        break;
                    }

                    continue;
                }

                if (messages[i].Index == message.Index)
                {
                    position = -1;
                    break;
                }
                if (messages[i].Index < message.Index)
                {
                    position = i;
                    break;
                }
            }

            if (position != -1)
            {
                //message.IsAnimated = position == 0;
                Execute.BeginOnUIThread(() => messages.Insert(position, message));
            }

            return position;
        }

        public virtual void Update(TLDialog dialog)
        {
            Peer = dialog.Peer;
            UnreadCount = dialog.UnreadCount;

            //если последнее сообщение отправляется и имеет дату больше, то не меняем
            if (TopMessageId == null && TopMessage.DateIndex > dialog.TopMessage.DateIndex)
            {
                //добавляем сообщение в список в нужное место
                InsertMessageInOrder(Messages, dialog.TopMessage);

                return;
            }
            TopMessageId = dialog.TopMessageId;
            TopMessageRandomId = dialog.TopMessageRandomId;
            TopMessage = dialog.TopMessage;
            if (Messages.Count > 0)
            {
                for (int i = 0; i < Messages.Count; i++)
                {
                    if (Messages[i].DateIndex < TopMessage.DateIndex)
                    {
                        Messages.Insert(i, TopMessage);
                        break;
                    }
                    if (Messages[i].DateIndex == TopMessage.DateIndex)
                    {
                        break;
                    }
                }
            }
            else
            {
                Messages.Add(TopMessage);
            }
        }
    }

    public class TLDialog : TLDialogBase
    {
        public const uint Signature = TLConstructors.TLDialog;

        #region Additional

        public TLMessageBase _topMessage;

        public TLMessageBase TopMessage
        {
            get { return _topMessage; }
            set { SetField(ref _topMessage, value, () => TopMessage); }
        }

        public ObservableCollection<TLMessageBase> Messages { get; set; }

        public bool ShowFrom
        {
            get { return Peer is TLPeerChat && !(TopMessage is TLMessageService); }
        }

        #endregion

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLPeerBase>(bytes, ref position);
            TopMessageId = GetObject<TLInt>(bytes, ref position);
            UnreadCount = GetObject<TLInt>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLPeerBase>(input);
            var topMessageId = GetObject<TLInt>(input);
            if (topMessageId.Value != 0)
            {
                TopMessageId = topMessageId;
            }

            UnreadCount = GetObject<TLInt>(input);

            var notifySettingsObject = GetObject<TLObject>(input);
            NotifySettings = notifySettingsObject as TLPeerNotifySettingsBase;

            var topMessageRandomId = GetObject<TLLong>(input);
            if (topMessageRandomId.Value != 0)
            {
                TopMessageRandomId = topMessageRandomId;
            }
            
            _with = GetObject<TLObject>(input);
            if (_with is TLNull) { _with = null; }

            var messages = GetObject<TLVector<TLMessageBase>>(input);
            Messages = messages != null ? new ObservableCollection<TLMessageBase>(messages.Items) : new ObservableCollection<TLMessageBase>();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);

            TopMessageId = TopMessageId ?? new TLInt(0);
            TopMessageId.ToStream(output);
            
            output.Write(UnreadCount.ToBytes());

            NotifySettings.NullableToStream(output);

            TopMessageRandomId = TopMessageRandomId ?? new TLLong(0);
            TopMessageRandomId.ToStream(output);

            With.NullableToStream(output);
            if (Messages != null)
            {
                var messages = new TLVector<TLMessageBase>{Items = Messages};
                messages.ToStream(output);
            }
            else
            {
                var messages = new TLVector<TLMessageBase>();
                messages.ToStream(output);
            }
        }


        public virtual void Update(TLDialog dialog)
        {
            Peer = dialog.Peer;
            UnreadCount = dialog.UnreadCount;

            //если последнее сообщение отправляется и имеет дату больше, то не меняем
            if (TopMessageId == null && (TopMessage == null || TopMessage.DateIndex > dialog.TopMessage.DateIndex))
            {


                //добавляем сообщение в список в нужное место, если его еще нет
                var insertRequired = false;
                if (Messages != null && dialog.TopMessage != null)
                {
                    var oldMessage = Messages.FirstOrDefault(x => x.Index == dialog.TopMessage.Index);
                    if (oldMessage == null)
                    {
                        insertRequired = true;
                    }
                }

                if (insertRequired)
                {
                    InsertMessageInOrder(Messages, dialog.TopMessage);
                }

                return;
            }
            TopMessageId = dialog.TopMessageId;
            TopMessageRandomId = dialog.TopMessageRandomId;
            TopMessage = dialog.TopMessage;

            lock (MessagesSyncRoot)
            {
                if (Messages.Count > 0)
                {
                    for (int i = 0; i < Messages.Count; i++)
                    {
                        if (Messages[i].DateIndex < TopMessage.DateIndex)
                        {
                            Messages.Insert(i, TopMessage);
                            break;
                        }
                        if (Messages[i].DateIndex == TopMessage.DateIndex)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    Messages.Add(TopMessage);
                }
            }
        }

        #region Methods

        public override int GetDateIndex()
        {
            return _topMessage != null ? _topMessage.DateIndex : 0;
        }

        public override int CountMessages()
        {
            return Messages.Count;
        }

        public override string ToString()
        {
            return string.Format("Index {5}, Peer {0}, IsChat {1}, UnreadCount {2}, TopMsgId {3}, TopMessage {4}", With ?? Peer,
                Peer is TLPeerChat, UnreadCount, TopMessageId, TopMessage, Index);
        }

        public static int InsertMessageInOrder(IList<TLMessageBase> messages, TLMessageBase message)
        {
            var position = -1;

            if (messages.Count == 0)
            {
                position = 0;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].Index == 0)
                {
                    if (messages[i].DateIndex < message.DateIndex)
                    {
                        position = i;
                        break;
                    }

                    continue;
                }

                if (messages[i].Index == message.Index)
                {
                    position = -1;
                    break;
                }
                if (messages[i].Index < message.Index)
                {
                    position = i;
                    break;
                }
            }

            if (position != -1)
            {
                //message._isAnimated = position == 0;
                messages.Insert(position, message);
            }

            return position;
        }
        #endregion
    }

    public class TLDialog24 : TLDialog
    {
        public new const uint Signature = TLConstructors.TLDialog24;

        public TLInt ReadInboxMaxId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLPeerBase>(bytes, ref position);
            TopMessageId = GetObject<TLInt>(bytes, ref position);
            ReadInboxMaxId = GetObject<TLInt>(bytes, ref position);
            UnreadCount = GetObject<TLInt>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLPeerBase>(input);
            var topMessageId = GetObject<TLInt>(input);
            if (topMessageId.Value != 0)
            {
                TopMessageId = topMessageId;
            }
            ReadInboxMaxId = GetObject<TLInt>(input);
            UnreadCount = GetObject<TLInt>(input);

            var notifySettingsObject = GetObject<TLObject>(input);
            NotifySettings = notifySettingsObject as TLPeerNotifySettingsBase;

            var topMessageRandomId = GetObject<TLLong>(input);
            if (topMessageRandomId.Value != 0)
            {
                TopMessageRandomId = topMessageRandomId;
            }

            _with = GetObject<TLObject>(input);
            if (_with is TLNull) { _with = null; }

            var messages = GetObject<TLVector<TLMessageBase>>(input);
            Messages = messages != null ? new ObservableCollection<TLMessageBase>(messages.Items) : new ObservableCollection<TLMessageBase>();

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);

            TopMessageId = TopMessageId ?? new TLInt(0);
            TopMessageId.ToStream(output);

            ReadInboxMaxId = ReadInboxMaxId ?? new TLInt(0);
            ReadInboxMaxId.ToStream(output);

            output.Write(UnreadCount.ToBytes());

            NotifySettings.NullableToStream(output);

            TopMessageRandomId = TopMessageRandomId ?? new TLLong(0);
            TopMessageRandomId.ToStream(output);

            With.NullableToStream(output);
            if (Messages != null)
            {
                var messages = new TLVector<TLMessageBase> { Items = Messages };
                messages.ToStream(output);
            }
            else
            {
                var messages = new TLVector<TLMessageBase>();
                messages.ToStream(output);
            }
        }

        public override void Update(TLDialog dialog)
        {
            try
            {
                base.Update(dialog);
            }
            catch (Exception ex)
            {
                
            }

            var dialog24 = dialog as TLDialog24;
            if (dialog24 != null)
            {
                ReadInboxMaxId = dialog24.ReadInboxMaxId;
            }
        }
    }

    public class TLDialogChannel : TLDialog24
    {
        public new const uint Signature = TLConstructors.TLDialogChannel;

        public TLInt TopImportantMessageId { get; set; }

        public TLInt UnreadImportantCount { get; set; }

        public TLInt Pts { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLPeerBase>(bytes, ref position);
            TopMessageId = GetObject<TLInt>(bytes, ref position);
            TopImportantMessageId = GetObject<TLInt>(bytes, ref position);
            ReadInboxMaxId = GetObject<TLInt>(bytes, ref position);
            UnreadCount = GetObject<TLInt>(bytes, ref position);
            UnreadImportantCount = GetObject<TLInt>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            try
            {
                Peer = GetObject<TLPeerBase>(input);
                var topMessageId = GetObject<TLInt>(input);
                if (topMessageId.Value != 0)
                {
                    TopMessageId = topMessageId;
                }
                var topImportantMessageId = GetObject<TLInt>(input);
                if (topImportantMessageId.Value != 0)
                {
                    TopImportantMessageId = topImportantMessageId;
                }
                ReadInboxMaxId = GetObject<TLInt>(input);
                UnreadCount = GetObject<TLInt>(input);
                UnreadImportantCount = GetObject<TLInt>(input);

                var notifySettingsObject = GetObject<TLObject>(input);
                NotifySettings = notifySettingsObject as TLPeerNotifySettingsBase;
                Pts = GetObject<TLInt>(input);

                var topMessageRandomId = GetObject<TLLong>(input);
                if (topMessageRandomId.Value != 0)
                {
                    TopMessageRandomId = topMessageRandomId;
                }

                _with = GetObject<TLObject>(input);
                if (_with is TLNull) { _with = null; }

                var messages = GetObject<TLVector<TLMessageBase>>(input);
                Messages = messages != null ? new ObservableCollection<TLMessageBase>(messages.Items) : new ObservableCollection<TLMessageBase>();

            }
            catch (Exception ex)
            {
                
            }
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);

            TopMessageId = TopMessageId ?? new TLInt(0);
            TopMessageId.ToStream(output);
            TopImportantMessageId = TopImportantMessageId ?? new TLInt(0);
            TopImportantMessageId.ToStream(output);

            ReadInboxMaxId = ReadInboxMaxId ?? new TLInt(0);
            ReadInboxMaxId.ToStream(output);

            output.Write(UnreadCount.ToBytes());
            output.Write(UnreadImportantCount.ToBytes());

            NotifySettings.NullableToStream(output);

            output.Write(Pts.ToBytes());

            TopMessageRandomId = TopMessageRandomId ?? new TLLong(0);
            TopMessageRandomId.ToStream(output);

            With.NullableToStream(output);
            if (Messages != null)
            {
                var messages = new TLVector<TLMessageBase> { Items = Messages };
                messages.ToStream(output);
            }
            else
            {
                var messages = new TLVector<TLMessageBase>();
                messages.ToStream(output);
            }
        }

        public override void Update(TLDialog dialog)
        {
            try
            {
                base.Update(dialog);
            }
            catch (Exception ex)
            {

            }

            var d = dialog as TLDialogChannel;
            if (d != null)
            {
                TopImportantMessageId = d.TopImportantMessageId;
                UnreadImportantCount = d.UnreadImportantCount;
                Pts = d.Pts;
            }
        }
    }
}
