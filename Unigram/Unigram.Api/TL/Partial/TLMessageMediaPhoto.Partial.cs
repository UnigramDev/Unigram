using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public partial class TLMessageMediaPhoto : ITLMessageMediaCaption, ITLMessageMediaDestruct
    {
        public DateTime? DestructDate { get; set; }

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
                    RaisePropertyChanged(() => DestructProgress);
                }
            }
        }

        public Double DestructProgress
        {
            get
            {
                return (double)(DestructIn ?? 0) / (double)(TTLSeconds ?? 0);
            }
        }
    }
}
