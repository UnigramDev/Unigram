using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Telegram.Api.TL
{
    [DataContract]
    public class TLConfig : TLObject
    {
        public const uint Signature = TLConstructors.TLConfig;

        [DataMember]
        public TLInt Date { get; set; }

        [DataMember]
        public TLBool TestMode { get; set; }

        /// <summary>
        /// Номер датацентра, ему может соответствовать несколько записей в DCOptions
        /// </summary>
        [DataMember]
        public TLInt ThisDC { get; set; }

        [DataMember]
        public TLVector<TLDCOption> DCOptions { get; set; }

        [DataMember]
        public TLInt ChatSizeMax { get; set; }

        [DataMember]
        public TLInt BroadcastSizeMax { get; set; }
       
        #region Additional
        /// <summary>
        /// Время последней загрузки config
        /// </summary>
        [DataMember]
        public DateTime LastUpdate { get; set; }

        /// <summary>
        /// Номер конкретного датацентра внутри списка DCOptions, однозначно определяет текущий датацентр
        /// </summary>
        [DataMember]
        public int ActiveDCOptionIndex { get; set; }

        [DataMember]
        public string Country { get; set; }
        #endregion

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);
            TestMode = GetObject<TLBool>(bytes, ref position);
            ThisDC = GetObject<TLInt>(bytes, ref position);
            DCOptions = GetObject<TLVector<TLDCOption>>(bytes, ref position);
            ChatSizeMax = GetObject<TLInt>(bytes, ref position);
            BroadcastSizeMax = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public static TLConfig Merge(TLConfig oldConfig, TLConfig newConfig)
        {
            if (oldConfig == null)
                return newConfig;

            if (newConfig == null)
                return oldConfig; 

            foreach (var dcOption in oldConfig.DCOptions)
            {
                if (dcOption.AuthKey != null)
                {
                    var option = dcOption;
                    foreach (var newDCOption in newConfig.DCOptions.Where(x => x.AreEquals(option)))
                    {
                        newDCOption.AuthKey = dcOption.AuthKey;
                        newDCOption.Salt = dcOption.Salt;
                        newDCOption.SessionId = dcOption.SessionId;
                        newDCOption.ClientTicksDelta = dcOption.ClientTicksDelta;
                    }
                }
            }
            if (!string.IsNullOrEmpty(oldConfig.Country))
            {
                newConfig.Country = oldConfig.Country;
            }
            if (oldConfig.ActiveDCOptionIndex != default(int))
            {
                var oldActiveDCOption = oldConfig.DCOptions[oldConfig.ActiveDCOptionIndex];
                var dcId = oldConfig.DCOptions[oldConfig.ActiveDCOptionIndex].Id.Value;
                var ipv6 = oldActiveDCOption.IPv6.Value;
                var media = oldActiveDCOption.Media.Value;

                TLDCOption newActiveDCOption = null;
                int newActiveDCOptionIndex = 0;
                for (var i = 0; i < newConfig.DCOptions.Count; i++)
                {
                    if (newConfig.DCOptions[i].Id.Value == dcId
                        && newConfig.DCOptions[i].IPv6.Value == ipv6
                        && newConfig.DCOptions[i].Media.Value == media)
                    {
                        newActiveDCOption = newConfig.DCOptions[i];
                        newActiveDCOptionIndex = i;
                        break;
                    }
                }

                if (newActiveDCOption == null)
                {
                    for (var i = 0; i < newConfig.DCOptions.Count; i++)
                    {
                        if (newConfig.DCOptions[i].Id.Value == dcId)
                        {
                            newActiveDCOption = newConfig.DCOptions[i];
                            newActiveDCOptionIndex = i;
                            break;
                        }
                    }
                }

                newConfig.ActiveDCOptionIndex = newActiveDCOptionIndex;
            }
            if (oldConfig.LastUpdate != default(DateTime))
            {
                newConfig.LastUpdate = oldConfig.LastUpdate;
            }

            return newConfig;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format("Date utc0 {0} {1}", Date.Value, TLUtils.ToDateTime(Date).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("TestMode {0}", TestMode));
            sb.AppendLine(string.Format("ChatSizeMax {0}", ChatSizeMax));
            sb.AppendLine(string.Format("BroadcastSizeMax {0}", BroadcastSizeMax));

            return sb.ToString();
        }
    }

    [DataContract]
    public class TLConfig23 : TLConfig
    {
        public new const uint Signature = TLConstructors.TLConfig23;

        [DataMember]
        public TLInt Expires { get; set; }

        [DataMember]
        public TLInt ChatBigSize { get; set; }

        [DataMember]
        public TLVector<TLDisabledFeature> DisabledFeatures { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);
            Expires = GetObject<TLInt>(bytes, ref position);
            TestMode = GetObject<TLBool>(bytes, ref position);
            ThisDC = GetObject<TLInt>(bytes, ref position);
            DCOptions = GetObject<TLVector<TLDCOption>>(bytes, ref position);
            ChatBigSize = GetObject<TLInt>(bytes, ref position);
            ChatSizeMax = GetObject<TLInt>(bytes, ref position);
            BroadcastSizeMax = GetObject<TLInt>(bytes, ref position);
            DisabledFeatures = GetObject<TLVector<TLDisabledFeature>>(bytes, ref position);

            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format("Date utc0 {0} {1}", Date.Value, TLUtils.ToDateTime(Date).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("Expires utc0 {0} {1}", Expires.Value, TLUtils.ToDateTime(Expires).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("TestMode {0}", TestMode));
            sb.AppendLine(string.Format("ChatBigSize {0}", ChatBigSize));
            sb.AppendLine(string.Format("ChatSizeMax {0}", ChatSizeMax));
            sb.AppendLine(string.Format("BroadcastSizeMax {0}", BroadcastSizeMax));
            sb.AppendLine(string.Format("DisabledFeatures {0}", DisabledFeatures.Count));

            return sb.ToString();
        }
    }

    [DataContract]
    public class TLConfig24 : TLConfig23
    {
        public new const uint Signature = TLConstructors.TLConfig24;

        [DataMember]
        public TLInt OnlineUpdatePeriodMs { get; set; }

        [DataMember]
        public TLInt OfflineBlurTimeoutMs { get; set; }

        [DataMember]
        public TLInt OfflineIdleTimeoutMs { get; set; }

        [DataMember]
        public TLInt OnlineCloudTimeoutMs { get; set; }

        [DataMember]
        public TLInt NotifyCloudDelayMs { get; set; }

        [DataMember]
        public TLInt NotifyDefaultDelayMs { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);
            Expires = GetObject<TLInt>(bytes, ref position);
            TestMode = GetObject<TLBool>(bytes, ref position);
            ThisDC = GetObject<TLInt>(bytes, ref position);
            DCOptions = GetObject<TLVector<TLDCOption>>(bytes, ref position);
            ChatSizeMax = GetObject<TLInt>(bytes, ref position);
            BroadcastSizeMax = GetObject<TLInt>(bytes, ref position);
            OnlineUpdatePeriodMs = GetObject<TLInt>(bytes, ref position);
            OfflineBlurTimeoutMs = GetObject<TLInt>(bytes, ref position);
            OfflineIdleTimeoutMs = GetObject<TLInt>(bytes, ref position);
            OnlineCloudTimeoutMs = GetObject<TLInt>(bytes, ref position);
            NotifyCloudDelayMs = GetObject<TLInt>(bytes, ref position);
            NotifyDefaultDelayMs = GetObject<TLInt>(bytes, ref position);
            ChatBigSize = GetObject<TLInt>(bytes, ref position);
            DisabledFeatures = GetObject<TLVector<TLDisabledFeature>>(bytes, ref position);

            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format("Date utc0 {0} {1}", Date.Value, TLUtils.ToDateTime(Date).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("Expires utc0 {0} {1}", Expires.Value, TLUtils.ToDateTime(Expires).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("TestMode {0}", TestMode));
            sb.AppendLine(string.Format("ChatSizeMax {0}", ChatSizeMax));
            sb.AppendLine(string.Format("BroadcastSizeMax {0}", BroadcastSizeMax));
            sb.AppendLine(string.Format("OnlineUpdatePeriodMs {0}", OnlineUpdatePeriodMs));
            sb.AppendLine(string.Format("OfflineBlurTimeoutMs {0}", OfflineBlurTimeoutMs));
            sb.AppendLine(string.Format("OfflineIdleTimeoutMs {0}", OfflineIdleTimeoutMs));
            sb.AppendLine(string.Format("OnlineCloudTimeoutMs {0}", OnlineCloudTimeoutMs));
            sb.AppendLine(string.Format("NotifyCloudDelayMs {0}", NotifyCloudDelayMs));
            sb.AppendLine(string.Format("NotifyDefaultDelayMs {0}", NotifyDefaultDelayMs));
            sb.AppendLine(string.Format("ChatBigSize {0}", ChatBigSize));
            sb.AppendLine(string.Format("DisabledFeatures {0}", DisabledFeatures.Count));

            return sb.ToString();
        }
    }

    [DataContract]
    public class TLConfig26 : TLConfig24
    {
        public new const uint Signature = TLConstructors.TLConfig26;

        [DataMember]
        public TLInt ForwardedCountMax { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);
            Expires = GetObject<TLInt>(bytes, ref position);
            TestMode = GetObject<TLBool>(bytes, ref position);
            ThisDC = GetObject<TLInt>(bytes, ref position);
            DCOptions = GetObject<TLVector<TLDCOption>>(bytes, ref position);
            ChatSizeMax = GetObject<TLInt>(bytes, ref position);
            BroadcastSizeMax = GetObject<TLInt>(bytes, ref position);
            ForwardedCountMax = GetObject<TLInt>(bytes, ref position);
            OnlineUpdatePeriodMs = GetObject<TLInt>(bytes, ref position);
            OfflineBlurTimeoutMs = GetObject<TLInt>(bytes, ref position);
            OfflineIdleTimeoutMs = GetObject<TLInt>(bytes, ref position);
            OnlineCloudTimeoutMs = GetObject<TLInt>(bytes, ref position);
            NotifyCloudDelayMs = GetObject<TLInt>(bytes, ref position);
            NotifyDefaultDelayMs = GetObject<TLInt>(bytes, ref position);
            ChatBigSize = GetObject<TLInt>(bytes, ref position);
            DisabledFeatures = GetObject<TLVector<TLDisabledFeature>>(bytes, ref position);

            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format("Date utc0 {0} {1}", Date.Value, TLUtils.ToDateTime(Date).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("Expires utc0 {0} {1}", Expires.Value, TLUtils.ToDateTime(Expires).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("TestMode {0}", TestMode));
            sb.AppendLine(string.Format("ChatSizeMax {0}", ChatSizeMax));
            sb.AppendLine(string.Format("BroadcastSizeMax {0}", BroadcastSizeMax));
            sb.AppendLine(string.Format("ForwardedCountMax {0}", ForwardedCountMax));
            sb.AppendLine(string.Format("OnlineUpdatePeriodMs {0}", OnlineUpdatePeriodMs));
            sb.AppendLine(string.Format("OfflineBlurTimeoutMs {0}", OfflineBlurTimeoutMs));
            sb.AppendLine(string.Format("OfflineIdleTimeoutMs {0}", OfflineIdleTimeoutMs));
            sb.AppendLine(string.Format("OnlineCloudTimeoutMs {0}", OnlineCloudTimeoutMs));
            sb.AppendLine(string.Format("NotifyCloudDelayMs {0}", NotifyCloudDelayMs));
            sb.AppendLine(string.Format("NotifyDefaultDelayMs {0}", NotifyDefaultDelayMs));
            sb.AppendLine(string.Format("ChatBigSize {0}", ChatBigSize));
            sb.AppendLine(string.Format("DisabledFeatures {0}", DisabledFeatures.Count));

            return sb.ToString();
        }
    }

    [DataContract]
    public class TLConfig28 : TLConfig26
    {
        public new const uint Signature = TLConstructors.TLConfig28;

        [DataMember]
        public TLInt PushChatPeriodMs { get; set; }

        [DataMember]
        public TLInt PushChatLimit { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);
            Expires = GetObject<TLInt>(bytes, ref position);
            TestMode = GetObject<TLBool>(bytes, ref position);
            ThisDC = GetObject<TLInt>(bytes, ref position);
            DCOptions = GetObject<TLVector<TLDCOption>>(bytes, ref position);
            ChatSizeMax = GetObject<TLInt>(bytes, ref position);
            BroadcastSizeMax = GetObject<TLInt>(bytes, ref position);
            ForwardedCountMax = GetObject<TLInt>(bytes, ref position);
            OnlineUpdatePeriodMs = GetObject<TLInt>(bytes, ref position);
            OfflineBlurTimeoutMs = GetObject<TLInt>(bytes, ref position);
            OfflineIdleTimeoutMs = GetObject<TLInt>(bytes, ref position);
            OnlineCloudTimeoutMs = GetObject<TLInt>(bytes, ref position);
            NotifyCloudDelayMs = GetObject<TLInt>(bytes, ref position);
            NotifyDefaultDelayMs = GetObject<TLInt>(bytes, ref position);
            ChatBigSize = GetObject<TLInt>(bytes, ref position);
            PushChatPeriodMs = GetObject<TLInt>(bytes, ref position);
            PushChatLimit = GetObject<TLInt>(bytes, ref position);
            DisabledFeatures = GetObject<TLVector<TLDisabledFeature>>(bytes, ref position);

            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format("Date utc0 {0} {1}", Date.Value, TLUtils.ToDateTime(Date).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("Expires utc0 {0} {1}", Expires.Value, TLUtils.ToDateTime(Expires).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("TestMode {0}", TestMode));
            sb.AppendLine(string.Format("ChatSizeMax {0}", ChatSizeMax));
            sb.AppendLine(string.Format("BroadcastSizeMax {0}", BroadcastSizeMax));
            sb.AppendLine(string.Format("ForwardedCountMax {0}", ForwardedCountMax));
            sb.AppendLine(string.Format("OnlineUpdatePeriodMs {0}", OnlineUpdatePeriodMs));
            sb.AppendLine(string.Format("OfflineBlurTimeoutMs {0}", OfflineBlurTimeoutMs));
            sb.AppendLine(string.Format("OfflineIdleTimeoutMs {0}", OfflineIdleTimeoutMs));
            sb.AppendLine(string.Format("OnlineCloudTimeoutMs {0}", OnlineCloudTimeoutMs));
            sb.AppendLine(string.Format("NotifyCloudDelayMs {0}", NotifyCloudDelayMs));
            sb.AppendLine(string.Format("NotifyDefaultDelayMs {0}", NotifyDefaultDelayMs));
            sb.AppendLine(string.Format("ChatBigSize {0}", ChatBigSize));
            sb.AppendLine(string.Format("PushChatPeriodMs {0}", PushChatPeriodMs));
            sb.AppendLine(string.Format("PushChatLimit {0}", PushChatLimit));
            sb.AppendLine(string.Format("DisabledFeatures {0}", DisabledFeatures.Count));

            return sb.ToString();
        }
    }

    [DataContract]
    public class TLConfig41 : TLConfig28
    {
        public new const uint Signature = TLConstructors.TLConfig41;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);
            Expires = GetObject<TLInt>(bytes, ref position);
            TestMode = GetObject<TLBool>(bytes, ref position);
            ThisDC = GetObject<TLInt>(bytes, ref position);
            DCOptions = GetObject<TLVector<TLDCOption>>(bytes, ref position);
            ChatSizeMax = GetObject<TLInt>(bytes, ref position);
            BroadcastSizeMax = GetObject<TLInt>(bytes, ref position);
            ForwardedCountMax = GetObject<TLInt>(bytes, ref position);
            OnlineUpdatePeriodMs = GetObject<TLInt>(bytes, ref position);
            OfflineBlurTimeoutMs = GetObject<TLInt>(bytes, ref position);
            OfflineIdleTimeoutMs = GetObject<TLInt>(bytes, ref position);
            OnlineCloudTimeoutMs = GetObject<TLInt>(bytes, ref position);
            NotifyCloudDelayMs = GetObject<TLInt>(bytes, ref position);
            NotifyDefaultDelayMs = GetObject<TLInt>(bytes, ref position);
            ChatBigSize = GetObject<TLInt>(bytes, ref position);
            PushChatPeriodMs = GetObject<TLInt>(bytes, ref position);
            PushChatLimit = GetObject<TLInt>(bytes, ref position);
            DisabledFeatures = GetObject<TLVector<TLDisabledFeature>>(bytes, ref position);

            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Format("Date utc0 {0} {1}", Date.Value, TLUtils.ToDateTime(Date).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("Expires utc0 {0} {1}", Expires.Value, TLUtils.ToDateTime(Expires).ToUniversalTime().ToString("HH:mm:ss.fff dd-MM-yyyy")));
            sb.AppendLine(string.Format("TestMode {0}", TestMode));
            sb.AppendLine(string.Format("ChatSizeMax {0}", ChatSizeMax));
            sb.AppendLine(string.Format("BroadcastSizeMax {0}", BroadcastSizeMax));
            sb.AppendLine(string.Format("ForwardedCountMax {0}", ForwardedCountMax));
            sb.AppendLine(string.Format("OnlineUpdatePeriodMs {0}", OnlineUpdatePeriodMs));
            sb.AppendLine(string.Format("OfflineBlurTimeoutMs {0}", OfflineBlurTimeoutMs));
            sb.AppendLine(string.Format("OfflineIdleTimeoutMs {0}", OfflineIdleTimeoutMs));
            sb.AppendLine(string.Format("OnlineCloudTimeoutMs {0}", OnlineCloudTimeoutMs));
            sb.AppendLine(string.Format("NotifyCloudDelayMs {0}", NotifyCloudDelayMs));
            sb.AppendLine(string.Format("NotifyDefaultDelayMs {0}", NotifyDefaultDelayMs));
            sb.AppendLine(string.Format("ChatBigSize {0}", ChatBigSize));
            sb.AppendLine(string.Format("PushChatPeriodMs {0}", PushChatPeriodMs));
            sb.AppendLine(string.Format("PushChatLimit {0}", PushChatLimit));
            sb.AppendLine(string.Format("DisabledFeatures {0}", DisabledFeatures.Count));

            return sb.ToString();
        }
    }
}
