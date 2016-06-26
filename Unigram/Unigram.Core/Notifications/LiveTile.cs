using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Unigram.Core.Notifications
{
    public class LiveTile
    {
        public static void Update(int unread, string lastMessageUser = null, string lastMessageContents = null)
        {

            XmlDocument tileXml = new XmlDocument();
            XmlElement tile = tileXml.CreateElement("tile");
            XmlElement visual = tileXml.CreateElement("visual");


            XmlElement medium = tileXml.CreateElement("binding");
            medium.SetAttribute("template", "TileMedium");
            XmlElement imageMediumPeek = tileXml.CreateElement("image");
            imageMediumPeek.SetAttribute("placement", "peek");
            imageMediumPeek.SetAttribute("src", "ms-appx:///Assets/Logos/Square150x150Logo/Square150x150Logo.png");
            medium.AppendChild(imageMediumPeek);
            XmlElement textMediumTitle = tileXml.CreateElement("text");
            textMediumTitle.SetAttribute("hint-wrap", "true");
            textMediumTitle.SetAttribute("hint-style", "caption");
            textMediumTitle.InnerText = lastMessageUser;
            medium.AppendChild(textMediumTitle);

            XmlElement textMediumPreview = tileXml.CreateElement("text");
            textMediumPreview.SetAttribute("hint-wrap", "true");
            //textMediumPreview.SetAttribute("hint-maxLines", "100");
            textMediumPreview.SetAttribute("hint-style", "captionSubtle");

            textMediumPreview.InnerText = lastMessageContents;
            medium.AppendChild(textMediumPreview);

            visual.AppendChild(medium);

            

            XmlElement wide = tileXml.CreateElement("binding");
            wide.SetAttribute("template", "TileWide");
            XmlElement imageWidePeek = tileXml.CreateElement("image");
            imageWidePeek.SetAttribute("placement", "peek");
            imageWidePeek.SetAttribute("src", "ms-appx:///Assets/Logos/Wide310x150Logo/Wide310x150Logo.png");
            wide.AppendChild(imageWidePeek);
            XmlElement textWideTitle = tileXml.CreateElement("text");
            textWideTitle.SetAttribute("hint-wrap", "true");
            textWideTitle.SetAttribute("hint-style", "caption");
            textWideTitle.InnerText = lastMessageUser;
            wide.AppendChild(textWideTitle);

            XmlElement textWidePreview = tileXml.CreateElement("text");
            textWidePreview.SetAttribute("hint-wrap", "true");
            //textWidePreview.SetAttribute("hint-maxLines", "100");
            textWidePreview.SetAttribute("hint-style", "captionSubtle");
            textWidePreview.InnerText = lastMessageContents;
            wide.AppendChild(textWidePreview);

            visual.AppendChild(wide);

            XmlElement large = tileXml.CreateElement("binding");
            large.SetAttribute("template", "TileLarge");
            XmlElement imageLargePeek = tileXml.CreateElement("image");
            imageLargePeek.SetAttribute("placement", "peek");
            imageLargePeek.SetAttribute("src", "ms-appx:///Assets/Logos/Square150x150Logo/Square150x150Logo.png");
            large.AppendChild(imageLargePeek);
            XmlElement textLargeTitle = tileXml.CreateElement("text");
            textLargeTitle.SetAttribute("hint-wrap", "true");
            textLargeTitle.SetAttribute("hint-style", "caption");
            textLargeTitle.InnerText = lastMessageUser;
            large.AppendChild(textLargeTitle);

            XmlElement textLargePreview = tileXml.CreateElement("text");
            textLargePreview.SetAttribute("hint-wrap", "true");
            textLargePreview.SetAttribute("hint-maxLines", "100");
            textLargePreview.SetAttribute("hint-style", "captionSubtle");
            textLargePreview.InnerText = lastMessageContents;
            large.AppendChild(textLargePreview);

            visual.AppendChild(large);

            tile.AppendChild(visual);
            tileXml.AppendChild(tile);


            TileNotification tileNotification = new TileNotification(tileXml);
            TileUpdater tileUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
            tileUpdater.Update(tileNotification);


            XmlDocument badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            XmlElement badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
            badgeElement.SetAttribute("value", unread + "");
            BadgeNotification badge = new BadgeNotification(badgeXml);
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badge);
        }

        public static void Clear()
        {

            XmlDocument badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            BadgeNotification badge = new BadgeNotification(badgeXml);
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badge);

            TileUpdater tileUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
            tileUpdater.Clear();
        }
    }
}
