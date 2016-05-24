namespace Telegram.Api.TL.Functions.Help
{
    public class TLInvokeWithLayer : TLObject
    {
        public const string Signature = "#da9b0d0d";

        public TLInt Layer { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature), 
                Layer.ToBytes());
        }
    }

    public class TLInvokeWithLayerN : TLObject
    {
        public const string Signature2 = "#289dd1f6";

        public const string Signature3 = "#b7475268";

        public const string Signature4 = "#dea0d430";

        public const string Signature5 = "#417a57ae";

        public const string Signature6 = "#3a64d54d";

        public const string Signature7 = "#a5be56d3";

        public const string Signature8 = "#e9abd9fd";

        public const string Signature9 = "#76715a63";

        public const string Signature10 = "#39620c41";

        public const string Signature11 = "#a6b88fdf";

        public const string Signature12 = "#dda60d3c";

        public const string Signature13 = "#427c8ea2";

        public const string Signature14 = "#2b9b08fa";

        public const string Signature15 = "#b4418b64";

        public const string Signature16 = "#cf5f0987";

        public const string Signature17 = "#50858a19";

        public const string Signature18 = "#1c900537";

        //public const string Signature19 = "#da9b0d0d";

        public TLObject Data { get; set; }

        public override byte[] ToBytes()
        {
            byte[] signature;

            if (Constants.SupportedLayer == 41)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(41) }.ToBytes();
            }

            if (Constants.SupportedLayer == 40)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(40) }.ToBytes();
            }

            if (Constants.SupportedLayer == 39)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(39) }.ToBytes();
            }

            if (Constants.SupportedLayer == 38)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(38) }.ToBytes();
            }

            if (Constants.SupportedLayer == 37)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(37) }.ToBytes();
            }

            if (Constants.SupportedLayer == 36)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(36) }.ToBytes();
            }

            if (Constants.SupportedLayer == 35)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(35) }.ToBytes();
            }

            if (Constants.SupportedLayer == 34)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(34) }.ToBytes();
            }

            if (Constants.SupportedLayer == 33)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(33) }.ToBytes();
            }

            if (Constants.SupportedLayer == 32)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(32) }.ToBytes();
            }

            if (Constants.SupportedLayer == 31)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(31) }.ToBytes();
            }


            if (Constants.SupportedLayer == 30)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(30) }.ToBytes();
            }

            if (Constants.SupportedLayer == 29)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(29) }.ToBytes();
            }

            if (Constants.SupportedLayer == 28)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(28) }.ToBytes();
            }

            if (Constants.SupportedLayer == 27)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(27) }.ToBytes();
            }

            if (Constants.SupportedLayer == 26)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(26) }.ToBytes();
            }

            if (Constants.SupportedLayer == 25)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(25) }.ToBytes();
            }

            if (Constants.SupportedLayer == 24)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(24) }.ToBytes();
            }

            if (Constants.SupportedLayer == 23)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(23) }.ToBytes();
            }

            if (Constants.SupportedLayer == 22)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(22) }.ToBytes();
            }

            if (Constants.SupportedLayer == 21)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(21) }.ToBytes();
            }

            if (Constants.SupportedLayer == 20)
            {
                signature = new TLInvokeWithLayer { Layer = new TLInt(20) }.ToBytes();
            }

            if (Constants.SupportedLayer == 19)
            {
                signature = new TLInvokeWithLayer{ Layer = new TLInt(19) }.ToBytes();
            }

            if (Constants.SupportedLayer == 18)
            {
                signature = TLUtils.SignatureToBytes(Signature18);
            }

            if (Constants.SupportedLayer == 17)
            {
                signature = TLUtils.SignatureToBytes(Signature17);
            }

            if (Constants.SupportedLayer == 16)
            {
                signature = TLUtils.SignatureToBytes(Signature16);
            }

            if (Constants.SupportedLayer == 15)
            {
                signature = TLUtils.SignatureToBytes(Signature15);
            }

            if (Constants.SupportedLayer == 14)
            {
                signature = TLUtils.SignatureToBytes(Signature14);
            }

            if (Constants.SupportedLayer == 13)
            {
                signature = TLUtils.SignatureToBytes(Signature13);
            }

            if (Constants.SupportedLayer == 12)
            {
                signature = TLUtils.SignatureToBytes(Signature12);
            }

            if (Constants.SupportedLayer == 11)
            {
                signature = TLUtils.SignatureToBytes(Signature11);
            }
            
            if (Constants.SupportedLayer == 1)
            {
                signature = new byte[]{};
            }
            if (Constants.SupportedLayer == 2)
            {
                signature = TLUtils.SignatureToBytes(Signature2);
            }
            if (Constants.SupportedLayer == 3)
            {
                signature = TLUtils.SignatureToBytes(Signature3);
            }
            if (Constants.SupportedLayer == 4)
            {
                signature = TLUtils.SignatureToBytes(Signature4);
            }
            if (Constants.SupportedLayer == 5)
            {
                signature = TLUtils.SignatureToBytes(Signature5);
            }
            if (Constants.SupportedLayer == 6)
            {
                signature = TLUtils.SignatureToBytes(Signature6);
            }
            if (Constants.SupportedLayer == 7)
            {
                signature = TLUtils.SignatureToBytes(Signature7);
            }
            if (Constants.SupportedLayer == 8)
            {
                signature = TLUtils.SignatureToBytes(Signature8);
            }
            if (Constants.SupportedLayer == 9)
            {
                signature = TLUtils.SignatureToBytes(Signature9);
            }
            if (Constants.SupportedLayer == 10)
            {
                signature = TLUtils.SignatureToBytes(Signature10);
            }



            return TLUtils.Combine(signature, Data.ToBytes());
        }
    }
}
