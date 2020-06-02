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
