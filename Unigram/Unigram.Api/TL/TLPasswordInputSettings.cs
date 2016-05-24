using System;

namespace Telegram.Api.TL
{
    [Flags]
    public enum PasswordFlags
    {
        Password = 0x1,
        Email = 0x2,
    }

    public class TLPasswordInputSettings : TLObject
    {
        public const uint Signature = TLConstructors.TLPasswordInputSettings;

        private TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; } 
            set { _flags = value; }
        }

        private TLString _newSalt;

        public TLString NewSalt
        {
            get { return _newSalt; }
            set
            {
                _newSalt = value;
                Set(ref _flags, (int)PasswordFlags.Password);
            }
        }

        private TLString _newPasswordHash;

        public TLString NewPasswordHash
        {
            get { return _newPasswordHash; }
            set
            {
                _newPasswordHash = value; 
                Set(ref _flags, (int)PasswordFlags.Password);
            }
        }

        private TLString _hint;

        public TLString Hint
        {
            get { return _hint; }
            set
            {
                _hint = value;
                Set(ref _flags, (int)PasswordFlags.Password);
            }
        }

        private TLString _email;

        public TLString Email
        {
            get { return _email; }
            set
            {
                _email = value;
                Set(ref _flags, (int)PasswordFlags.Email);
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);

            if (IsSet(Flags, (int)PasswordFlags.Password))
            {
                NewSalt = GetObject<TLString>(bytes, ref position);
                NewPasswordHash = GetObject<TLString>(bytes, ref position);
                Hint = GetObject<TLString>(bytes, ref position);
            }
            if (IsSet(Flags, (int)PasswordFlags.Email))
            {
                Email = GetObject<TLString>(bytes, ref position);
            }

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Flags.ToBytes(),
                ToBytes(NewSalt, Flags, (int)PasswordFlags.Password),
                ToBytes(NewPasswordHash, Flags, (int)PasswordFlags.Password),
                ToBytes(Hint, Flags, (int)PasswordFlags.Password),
                ToBytes(Email, Flags, (int)PasswordFlags.Email));
        }
    }
}