using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.UI.Xaml;

namespace Unigram.Triggers
{
    public class FullExperienceTrigger : StateTriggerBase
    {
        public FullExperienceTrigger()
        {
            SetActive(ApiInfo.IsFullExperience);
        }
    }
}
