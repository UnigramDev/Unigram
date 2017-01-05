using System;
using System.Text;

namespace Unigram.Core.Rtf.Write
{
    public class RtfSectionFooter : RtfBlockList
    {
        internal RtfSectionFooter(RtfSection parent)
            : base(true, true, true, true, true)
        {
            if(parent == null)
            {
                throw new Exception("Section footer can only be placed within a section ");
            }
        }

        public override string Render()
        {
            StringBuilder result = new StringBuilder();

            result.AppendLine(@"{\footerr \ltrpar \pard\plain");
            result.AppendLine(@"\par ");
            result.Append(base.Render());
            result.AppendLine(@"\par");
            result.AppendLine(@"}");

            return result.ToString();
        }
    }
}
