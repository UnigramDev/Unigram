using System;

namespace Telegram.Api.TL
{
    class SignatureAttribute : Attribute
    {
        public string Value { get; set; }

        public SignatureAttribute(string signature)
        {
            Value = signature;
        }
    }
}
