using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Unigram.Core.Notifications
{
    public class Toast
    {
        // This will change and id will be replaced by the TL object that abstracts all other
        public static void Create(string id, string title, string message = null, string logo = null, string image = null)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement toast = doc.CreateElement("toast");
            toast.SetAttribute("launch", id);
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
            input.SetAttribute("id", "message");
            input.SetAttribute("type", "text");
            input.SetAttribute("placeHolderContent", "reply here");
            actions.AppendChild(input);

            XmlElement action = doc.CreateElement("action");
            //This will be "foreground" just as we don't have a background task yet
            //action.SetAttribute("activationType", "background");
            action.SetAttribute("activationType", "foreground");
            action.SetAttribute("content", "reply");
            action.SetAttribute("arguments", id);
            action.SetAttribute("imageUri", "ms-appx:///Assets/Icons/Toast/send.png");
            action.SetAttribute("hint-inputId", "message");
            actions.AppendChild(action);

            toast.AppendChild(actions);

            doc.AppendChild(toast);

            ToastNotifier toastNotifier = ToastNotificationManager.CreateToastNotifier();
            ToastNotification toastNotification = new ToastNotification(doc);
            // The two lines above will change when we have the api
            toastNotification.Group = "message";
            //toastNotification.Tag = Tag;
            toastNotifier.Show(toastNotification);
        }
    }
}
