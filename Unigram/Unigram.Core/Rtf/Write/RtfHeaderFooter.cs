using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;

namespace Unigram.Core.Rtf.Write
{
    /// <summary>
    /// Summary description for RtfHeaderFooter
    /// </summary>
    public class RtfHeaderFooter : RtfBlockList
    {
        private HeaderFooterType _type;
        
        internal RtfHeaderFooter(HeaderFooterType type)
            : base(true, false, true, true, false)
        {
            _type = type;
        }

        public override string Render()
        {
            StringBuilder result = new StringBuilder();

            if (_type == HeaderFooterType.Header) {
                result.AppendLine(@"{\header");
            } else if (_type == HeaderFooterType.Footer) {
                result.AppendLine(@"{\footer");
            } else {
                throw new Exception("Invalid HeaderFooterType");
            }
            result.AppendLine();
            for (int i = 0; i < base._blocks.Count; i++) {
                if (base._defaultCharFormat != null
                    && ((RtfBlock)base._blocks[i]).DefaultCharFormat != null) {
                    ((RtfBlock)base._blocks[i]).DefaultCharFormat.copyFrom(base._defaultCharFormat);
                }
                result.AppendLine(((RtfBlock)_blocks[i]).Render());
            }
            result.AppendLine("}");
            return result.ToString();
        }
    }
}
