using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Unigram.Core.Rtf.Write
{
    /// <summary>
    /// Summary description for RtfFootnote
    /// </summary>
    public class RtfFootnote : RtfBlockList
    {
        private int _position;
        
        internal RtfFootnote(int position, int textLength)
            : base(true, false, false, true, false)
        {
            if (position < 0 || position >= textLength) {
                throw new Exception("Invalid footnote position: " + position
                                    + " (text length=" + textLength + ")");
            }
            _position = position;
        }
        
        internal int Position
        {
            get
            {
                return _position;
            }
        }
        
        public override string Render()
        {
            StringBuilder result = new StringBuilder();

            result.AppendLine(@"{\super\chftn}");
            result.AppendLine(@"{\footnote\plain\chftn");
            ((RtfBlock)base._blocks[base._blocks.Count - 1]).BlockTail = "}";
            result.Append(base.Render());
            result.AppendLine("}");
            return result.ToString();
        }
    }
}
