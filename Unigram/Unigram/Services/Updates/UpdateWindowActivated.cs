using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Services.Updates
{
    public class UpdateWindowActivated
    {
        public bool IsActive { get; set; }

        public UpdateWindowActivated(bool active)
        {
            IsActive = active;
        }
    }
}
