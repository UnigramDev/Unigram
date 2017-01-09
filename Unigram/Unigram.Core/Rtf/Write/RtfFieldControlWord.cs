using System;
using System.Collections.Generic;
using System.Text;

namespace Unigram.Core.Rtf.Write
{
    public class RtfFieldControlWord : RtfRenderable
    {
        public enum FieldType
        {
            None = 0,
            Page,
            NumPages,
            Date,
            Time,
        }
        
        private static string[] ControlWordPool = new string[] {
            // correspond with FiledControlWords enum
            "",
            @"{\field{\*\fldinst PAGE }}",
            @"{\field{\*\fldinst NUMPAGES }}",
            @"{\field{\*\fldinst DATE }}",
            @"{\field{\*\fldinst TIME }}"
        };
        
        private int _position;
        private FieldType _type;
        
        internal RtfFieldControlWord(int position, FieldType type)
        {
            _position = position;
            _type = type;
        }
        
        internal int Position
        {
            get {
                return _position;
            }
        }
        
        public override string Render()
        {
            return ControlWordPool[(int)_type];
        }
    }
}
