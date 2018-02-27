using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Windows.UI.Xaml;

namespace Telegram.Api.TL
{
    public abstract partial class TLMessageBase
    {
        public Int64? RandomId { get; set; }

        private bool _isFirst;
        public bool IsFirst
        {
            get
            {
                return _isFirst;
            }
            set
            {
                if (_isFirst != value)
                {
                    _isFirst = value;
                }
            }
        }

        private bool _isLast;
        public bool IsLast
        {
            get
            {
                return _isLast;
            }
            set
            {
                if (_isLast != value)
                {
                    _isLast = value;
                }
            }
        }
    }
}
