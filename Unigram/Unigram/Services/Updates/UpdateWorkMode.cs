using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Services.Updates
{
    public class UpdateWorkMode
    {
        public UpdateWorkMode(bool visible, bool enabled)
        {
            IsVisible = visible;
            IsEnabled = enabled;
        }

        public bool IsVisible { get; private set; }

        public bool IsEnabled { get; private set; }
    }
}
