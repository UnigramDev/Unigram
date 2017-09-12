using Windows.System.Profile;
using Windows.UI.Xaml;

namespace Unigram.Triggers
{
    class DeviceFamilyTrigger : StateTriggerBase
    {
        private string _currentDeviceFamily, _queriedDeviceFamily;

        public string DeviceFamily
        {
            get
            {
                return _queriedDeviceFamily;
            }

            set
            {
                _queriedDeviceFamily = value;
                _currentDeviceFamily = AnalyticsInfo.VersionInfo.DeviceFamily;
                SetActive(_queriedDeviceFamily == _currentDeviceFamily);
            }
        }
    }
}
