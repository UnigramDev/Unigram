using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;

namespace Unigram.Tasks
{
    public sealed class VoIPServiceTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        internal static VoIPServiceTask Current { get; private set; }
        internal static AppServiceConnection Connection { get; private set; }

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            try
            {
                _deferral = taskInstance.GetDeferral();

                var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;

                Connection = details.AppServiceConnection;
                Current = this;
                VoIPCallTask.Mediator.Initialize(details.AppServiceConnection);

                taskInstance.Canceled += (s, e) => Close();
                details.AppServiceConnection.ServiceClosed += (s, e) => Close();
            }
            catch (Exception e)
            {
                _deferral?.Complete();
            }
        }

        private void Close()
        {
            Current = null;
            Connection = null;
            _deferral?.Complete();
        }
    }
}
