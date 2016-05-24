using System;
using System.IO;
using System.Runtime.Serialization;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [KnownType(typeof (TLPeerNotifySettingsEmpty))]
    [KnownType(typeof (TLPeerNotifySettings))]
    [DataContract]
    public abstract class TLPeerNotifySettingsBase : TLObject
    {
        #region Additional
        public DateTime? LastNotificationTime { get; set; }
        #endregion
    }

    [DataContract]
    public class TLPeerNotifySettings : TLPeerNotifySettingsBase
    {
        public const uint Signature = TLConstructors.TLPeerNotifySettings;

        /// <summary>
        /// Date to mute notifications untill
        /// </summary>
        [DataMember]
        public TLInt MuteUntil { get; set; }

        /// <summary>
        /// Notification sound
        /// </summary>
        [DataMember]
        public TLString Sound { get; set; }
        
        /// <summary>
        /// True to show message text at notifications
        /// </summary>
        [DataMember]
        public TLBool ShowPreviews { get; set; }

        /// <summary>
        /// Events mask (All/Empty)
        /// </summary>
        [DataMember]
        public TLInt EventsMask { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            MuteUntil = GetObject<TLInt>(bytes, ref position);
            Sound = GetObject<TLString>(bytes, ref position);
            ShowPreviews = GetObject<TLBool>(bytes, ref position);
            EventsMask = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            MuteUntil = GetObject<TLInt>(input);
            Sound = GetObject<TLString>(input);
            ShowPreviews = GetObject<TLBool>(input);
            EventsMask = GetObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(MuteUntil.ToBytes());
            output.Write(Sound.ToBytes());
            output.Write(ShowPreviews.ToBytes());
            output.Write(EventsMask.ToBytes());
        }
    }

    [DataContract]
    public class TLPeerNotifySettingsEmpty : TLPeerNotifySettingsBase
    {
        public const uint Signature = TLConstructors.TLPeerNotifySettingsEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }
    }
}
