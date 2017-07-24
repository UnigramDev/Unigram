using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessageMediaPhoto : ITLMessageMediaCaption, ITLMessageMediaDestruct
    {
        public Int64? DestructDate { get; set; }

        private Int32? _destructIn;
        public Int32? DestructIn
        {
            get
            {
                return _destructIn = _destructIn ?? TTLSeconds;
            }
            set
            {
                if (_destructIn != value)
                {
                    _destructIn = value;
                    RaisePropertyChanged(() => DestructIn);
                }
            }
        }
    }
}
