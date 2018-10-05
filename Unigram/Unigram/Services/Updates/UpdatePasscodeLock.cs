using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Services.Updates
{
    public class UpdatePasscodeLock
    {
        public UpdatePasscodeLock(bool enabled, bool locked)
        {
            IsEnabled = enabled;
            IsLocked = locked;
        }

        public bool IsEnabled { get; private set; }
        public bool IsLocked { get; private set; }
    }
}
