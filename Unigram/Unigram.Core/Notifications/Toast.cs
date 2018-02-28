using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Core.Managers;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Unigram.Core.Notifications
{
    public class Toast
    {
        protected const string TaskName = "ToastBackgroundTask";
        protected const string TaskEndPoint = "Unigram.BackgroundTasks.Notifications.ToastBackgroundTask";

        public static async Task RegisterBackgroundTasks()
        {
            //BackgroundExecutionManager.RemoveAccess();

            //foreach (var t in BackgroundTaskRegistration.AllTasks)
            //{
            //    if (t.Value.Name == "NotificationTask")
            //    {
            //        t.Value.Unregister(false);
            //    }
            //}

            var access = await BackgroundExecutionManager.RequestAccessAsync();
            if (access == BackgroundAccessStatus.DeniedByUser || access == BackgroundAccessStatus.DeniedBySystemPolicy)
            {
                return;
            }

            // TODO: remove the "new" when releasing to the store
            await BackgroundTaskManager.RegisterAsync("NewNotificationTask", "Unigram.Native.Tasks.NotificationTask", new PushNotificationTrigger());
            await BackgroundTaskManager.RegisterAsync("InteractiveTask", "Unigram.Tasks.InteractiveTask", new ToastNotificationActionTrigger());
        }

        public static Dictionary<string, string> GetData(IActivatedEventArgs args)
        {
            if (args.Kind == ActivationKind.ToastNotification)
            {
                ToastNotificationActivatedEventArgs toastActivationArgs = args as ToastNotificationActivatedEventArgs;

                var dictionary = SplitArguments(toastActivationArgs.Argument);
                if (toastActivationArgs.UserInput != null && toastActivationArgs.UserInput.Count > 0)
                {
                    for (int i = 0; i < toastActivationArgs.UserInput.Count; i++)
                    {
                        dictionary.Add(toastActivationArgs.UserInput.Keys.ElementAt(i), toastActivationArgs.UserInput.Values.ElementAt(i).ToString());
                    }
                }

                return dictionary;
            }

            return null;
        }

        public static Dictionary<string, string> GetData(IBackgroundTaskInstance args)
        {
            if (args.TriggerDetails is ToastNotificationActionTriggerDetail)
            {
                ToastNotificationActionTriggerDetail details = args.TriggerDetails as ToastNotificationActionTriggerDetail;
                if (details == null)
                {
                    return null;
                }

                var dictionary = SplitArguments(details.Argument);
                if (details.UserInput != null && details.UserInput.Count > 0)
                {
                    for (int i = 0; i < details.UserInput.Count; i++)
                    {
                        dictionary.Add(details.UserInput.Keys.ElementAt(i), details.UserInput.Values.ElementAt(i).ToString());
                    }
                }

                return dictionary;
            }

            return null;
        }

        public static Dictionary<string, string> SplitArguments(string arguments)
        {
            var dictionary = new Dictionary<string, string>();
            if (arguments == null || arguments == string.Empty || !arguments.Contains("="))
            {
                return dictionary;
            }

            string[] items = arguments.Split('&');
            foreach (string item in items)
            {
                string[] pair = item.Split('=');
                dictionary.Add(pair[0], pair[1]);
            }

            return dictionary;
        }
    }
}
