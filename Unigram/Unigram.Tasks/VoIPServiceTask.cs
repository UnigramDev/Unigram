using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;

namespace Unigram.Tasks
{
    public sealed class VoIPServiceTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;

        public static VoIPServiceTask Current => _current;
        public static AppServiceConnection Connection => _connection;

        internal static VoIPServiceTask _current;
        internal static AppServiceConnection _connection;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            try
            {
                _deferral = taskInstance.GetDeferral();

                VoIPCallTask.Log("VoIPServiceTask started", "VoIPServiceTask started");

                var details = taskInstance.TriggerDetails as AppServiceTriggerDetails;

                _connection = details.AppServiceConnection;
                _current = this;
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
            VoIPCallTask.Log("Releasing background task", "Releasing VoIPCallTask");

            VoIPCallTask.Mediator.Initialize(null as AppServiceConnection);

            _current = null;
            _connection = null;
            _deferral?.Complete();
        }
    }
}
