using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Helpers;

namespace Telegram.Api.TL
{
    public abstract partial class TLUserBase
    {
        #region Add
        public virtual string FullName
        {
            get
            {
                return Id.ToString();
            }
        }

        public virtual object PhotoSelf
        {
            get
            {
                return this;
            }
        }

        #endregion
    }
}
