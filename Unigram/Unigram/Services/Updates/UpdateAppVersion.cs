using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Services.Updates
{
    public class UpdateAppVersion
    {
        public CloudUpdate Update { get; set; }

        public UpdateAppVersion(CloudUpdate update)
        {
            Update = update;
        }
    }
}
