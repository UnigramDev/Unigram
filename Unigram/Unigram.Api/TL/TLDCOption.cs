using System;
using System.IO;
using System.Runtime.Serialization;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    [Flags]
    public enum DCOptionFlags
    {
        IPv6 = 0x1,
        Media = 0x2,
    }

    [KnownType(typeof(TLDCOption30))]
    [DataContract]
    public class TLDCOption : TLObject
    {
        public const uint Signature = TLConstructors.TLDCOption;

        [DataMember]
        public TLInt Id { get; set; }

        [DataMember]
        public TLString Hostname { get; set; }

        [DataMember]
        public TLString IpAddress { get; set; }

        [DataMember]
        public TLInt Port { get; set; }

#region Additional
        public TLLong CustomFlags { get; set; }

        [DataMember]
        public byte[] AuthKey { get; set; }

        [DataMember]
        public TLLong Salt { get; set; }

        [DataMember]
        public long ClientTicksDelta { get; set; }

        //[DataMember] //Important this field initialize with random value on each app startup to avoid TLBadMessage result with 32, 33 code (incorrect MsgSeqNo)
        public TLLong SessionId { get; set; }

        public virtual TLBool IPv6
        {
            get { return TLBool.False; } 
            set { }
        }

        public virtual TLBool Media
        {
            get { return TLBool.False; } 
            set { }
        }

        public bool IsValidIPv4Option(TLInt dcId)
        {
            return !IPv6.Value && Id != null && Id.Value == dcId.Value;
        }
#endregion

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            Hostname = GetObject<TLString>(bytes, ref position);
            IpAddress = GetObject<TLString>(bytes, ref position);
            Port = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            Hostname.ToStream(output);
            IpAddress.ToStream(output);
            Port.ToStream(output);

            CustomFlags.NullableToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            Hostname = GetObject<TLString>(input);
            IpAddress = GetObject<TLString>(input);
            Port = GetObject<TLInt>(input);

            CustomFlags = GetNullableObject<TLLong>(input);

            return this;
        }

        public bool AreEquals(TLDCOption dcOption)
        {
            if (dcOption == null) return false;

            return Id.Value == dcOption.Id.Value;
        }

        public override string ToString()
        {
            return string.Format("{0}) {1}:{2} (AuthKey {3})\n  Salt {4} TicksDelta {5}", Id, IpAddress, Port, AuthKey != null, Salt, ClientTicksDelta);
        }
    }

    [DataContract]
    public class TLDCOption30 : TLDCOption
    {
        public const uint Signature = TLConstructors.TLDCOption30;

        private TLInt _flags;

        [DataMember]
        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public override TLBool IPv6
        {
            get { return new TLBool(IsSet(_flags, (int)DCOptionFlags.IPv6)); }
            set { SetUnset(ref _flags, value.Value, (int)DCOptionFlags.IPv6); }
        }

        public override TLBool Media
        {
            get { return new TLBool(IsSet(_flags, (int)DCOptionFlags.Media)); }
            set { SetUnset(ref _flags, value.Value, (int)DCOptionFlags.Media); }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            //Hostname = GetObject<TLString>(bytes, ref position);
            IpAddress = GetObject<TLString>(bytes, ref position);
            Port = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Id.ToStream(output);
            //Hostname.ToStream(output);
            IpAddress.ToStream(output);
            Port.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            Id = GetObject<TLInt>(input);
            //Hostname = GetObject<TLString>(input);
            IpAddress = GetObject<TLString>(input);
            Port = GetObject<TLInt>(input);

            return this;
        }

        public override string ToString()
        {
            return string.Format("{0}) {1}:{2} (AuthKey {3})\n  Salt {4} TicksDelta {5} IPv6 {6} Media {7}", Id, IpAddress, Port, AuthKey != null, Salt, ClientTicksDelta, IPv6, Media);
        }
    }
}