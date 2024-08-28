//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Background;
using Windows.System.Profile;
using Windows.UI.Notifications;

namespace Telegram.Common
{
    public partial class Toast
    {
        public static async Task RegisterBackgroundTasks()
        {
            return;

            try
            {
                //BackgroundExecutionManager.RemoveAccess();

                foreach (var t in BackgroundTaskRegistration.AllTasks)
                {
                    if (t.Value.Name is "NotificationTask" or "NewNotificationTask" or "InProcessNotificationTask")
                    {
                        t.Value.Unregister(false);
                    }
                    else if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Xbox"))
                    {
                        t.Value.Unregister(false);
                    }
                }

                if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Xbox"))
                {
                    BackgroundExecutionManager.RemoveAccess();
                    return;
                }

                var access = await BackgroundExecutionManager.RequestAccessAsync();
                if (access is BackgroundAccessStatus.DeniedByUser or BackgroundAccessStatus.DeniedBySystemPolicy)
                {
                    return;
                }

                //Register("InProcessNotificationTask", null, () => new PushNotificationTrigger());
                Register("NewInteractiveTask", null, () => new ToastNotificationActionTrigger());
                //BackgroundTaskManager.Register("InteractiveTask", "Telegram.Tasks.InteractiveTask", new ToastNotificationActionTrigger());
            }
            catch { }
        }

        private static bool Register(string name, string entryPoint, Func<IBackgroundTrigger> trigger, Action onCompleted = null)
        {
            //var access = await BackgroundExecutionManager.RequestAccessAsync();
            //if (access == BackgroundAccessStatus.DeniedByUser || access == BackgroundAccessStatus.DeniedBySystemPolicy)
            //{
            //    return false;
            //}
            try
            {
                foreach (var t in BackgroundTaskRegistration.AllTasks)
                {
                    if (t.Value.Name == name)
                    {
                        //t.Value.Unregister(false);
                        return false;
                    }
                }

                var builder = new BackgroundTaskBuilder();
                builder.Name = name;

                if (entryPoint != null)
                {
                    builder.TaskEntryPoint = entryPoint;
                }

                builder.SetTrigger(trigger());

                var registration = builder.Register();
                if (onCompleted != null)
                {
                    registration.Completed += (s, a) =>
                    {
                        onCompleted();
                    };
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int? GetSession(IActivatedEventArgs args)
        {
            string arguments = null;

            switch (args)
            {
                case ToastNotificationActivatedEventArgs toastNotification:
                    arguments = toastNotification.Argument;
                    break;
                case LaunchActivatedEventArgs launch:
                    if (launch.TileActivatedInfo != null && launch.TileActivatedInfo.RecentlyShownNotifications.Count > 0)
                    {
                        arguments = launch.TileActivatedInfo.RecentlyShownNotifications[0].Arguments;
                    }
                    break;
                case ProtocolActivatedEventArgs protocol:
                    var uri = protocol.Uri.ToString();
                    break;
            }

            var data = SplitArguments(arguments);
            if (data.TryGetValue("session", out string value) && int.TryParse(value, out int result))
            {
                // TODO: move additional checks here
                return result;
            }

            return null;
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

        public static Dictionary<string, string> GetData(ToastNotificationActionTriggerDetail triggerDetail)
        {
            var dictionary = SplitArguments(triggerDetail.Argument);
            if (triggerDetail.UserInput != null && triggerDetail.UserInput.Count > 0)
            {
                foreach (var input in triggerDetail.UserInput)
                {
                    dictionary[input.Key] = input.Value.ToString();
                }
            }

            return dictionary;
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
