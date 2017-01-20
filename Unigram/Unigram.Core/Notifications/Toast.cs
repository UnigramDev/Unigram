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
            BackgroundExecutionManager.RemoveAccess();

            foreach (var t in BackgroundTaskRegistration.AllTasks)
            {
                if (t.Value.Name == "NotificationTask")
                {
                    t.Value.Unregister(false);
                }
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
            if (arguments == null || arguments == string.Empty || !arguments.Contains("&"))
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

        // This will change and id will be replaced by the TL object that abstracts all other
        public static void Create(string id, string title, string message = null, string logo = null, string image = null)
        {
            string arguments = "messageId=" + id;
            XmlDocument doc = new XmlDocument();
            XmlElement toast = doc.CreateElement("toast");
            toast.SetAttribute("launch", arguments);
            toast.SetAttribute("activationType", "foreground ");

            XmlElement visual = doc.CreateElement("visual");

            XmlElement binding = doc.CreateElement("binding");
            binding.SetAttribute("template", "ToastGeneric");

            XmlElement textTitle = doc.CreateElement("text");
            textTitle.InnerText = title;
            binding.AppendChild(textTitle);

            if (message != null && message.Length > 0)
            {
                XmlElement textMessage = doc.CreateElement("text");
                textMessage.InnerText = message;
                binding.AppendChild(textMessage);
            }

            if (logo != null && logo.Length > 0)
            {
                XmlElement logoImage = doc.CreateElement("image");
                logoImage.SetAttribute("placement", "appLogoOverride");
                logoImage.SetAttribute("src", logo);
                logoImage.SetAttribute("hint-crop", "circle");
                binding.AppendChild(logoImage);
            }

            if (image != null && image.Length > 0)
            {
                XmlElement messageImage = doc.CreateElement("image");
                messageImage.SetAttribute("placement", "inline");
                messageImage.SetAttribute("src", image);
                binding.AppendChild(messageImage);
            }

            visual.AppendChild(binding);
            toast.AppendChild(visual);

            XmlElement actions = doc.CreateElement("actions");

            XmlElement input = doc.CreateElement("input");
            input.SetAttribute("id", "text");
            input.SetAttribute("type", "text");
            input.SetAttribute("placeHolderContent", Managers.ResourcesManager.GetString("Notifications-Toast-Reply.Text"));
            actions.AppendChild(input);

            XmlElement action = doc.CreateElement("action");
            action.SetAttribute("activationType", "background");
            action.SetAttribute("content", "reply");
            action.SetAttribute("arguments", arguments);
            action.SetAttribute("imageUri", "ms-appx:///Assets/Icons/Toast/send.png");
            action.SetAttribute("hint-inputId", "text");
            actions.AppendChild(action);

            toast.AppendChild(actions);

            doc.AppendChild(toast);

            ToastNotifier toastNotifier = ToastNotificationManager.CreateToastNotifier();
            ToastNotification toastNotification = new ToastNotification(doc);
            // The two lines bellow will change when we have the api
            toastNotification.Group = "message";
            //toastNotification.Tag = Tag;
            toastNotifier.Show(toastNotification);
        }
    }
}
