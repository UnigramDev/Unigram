using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLRichTextBase
    {
        public override string ToString()
        {
            return ToString(false);
        }

        public virtual string ToString(bool reserved)
        {
            throw new NotImplementedException();
        }
    }

    public partial class TLTextBold
    {
        public override string ToString(bool reserved)
        {
            return Text.ToString();
        }
    }

    public partial class TLTextConcat
    {
        public override string ToString(bool reserved)
        {
            var result = new string[Texts.Count];
			for (int i = 0; i < result.Length; i++)
            {
                result[i] = Texts[i].ToString();
            }

            return string.Join(string.Empty, result);
        }
    }

    public partial class TLTextEmail
    {
        public override string ToString(bool reserved)
        {
            return Text.ToString();
        }
    }

    public partial class TLTextEmpty
    {
        public override string ToString(bool reserved)
        {
            return string.Empty;   
        }
    }

    public partial class TLTextFixed
    {
        public override string ToString(bool reserved)
        {
            return Text.ToString();
        }
    }

    public partial class TLTextItalic
    {
        public override string ToString(bool reserved)
        {
            return Text.ToString();
        }
    }

    public partial class TLTextPlain
    {
        public override string ToString(bool reserved)
        {
            return Text;
        }
    }

    public partial class TLTextStrike
    {
        public override string ToString(bool reserved)
        {
            return Text.ToString();
        }
    }

    public partial class TLTextUnderline
    {
        public override string ToString(bool reserved)
        {
            return Text.ToString();
        }
    }

    public partial class TLTextUrl
    {
        public override string ToString(bool reserved)
        {
            return Text.ToString();
        }
    }
}
