namespace Unigram.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using Telegram.Api.Services.DeviceInfo;

    public abstract class PhoneInfoServiceBase : IDeviceInfoService
    {
        private const string AppManifestName = "WMAppManifest.xml";
        private const string AppNodeName = "App";

        public static string GetAppAttribute(string attributeName)
        {
            //try
            //{
            //    var settings = new XmlReaderSettings { XmlResolver = new XmlXapResolver() };

            //    using (var rdr = XmlReader.Create(AppManifestName, settings))
            //    {
            //        rdr.ReadToDescendant(AppNodeName);
            //        if (!rdr.IsStartElement())
            //        {
            //            throw new FormatException(AppManifestName + " is missing " + AppNodeName);
            //        }

            //        return rdr.GetAttribute(attributeName);
            //    }
            //}
            //catch (Exception)
            //{
            //    return String.Empty;
            //}
            return null;
        }

        public abstract string Model { get; }
        public abstract string AppVersion { get; }
        public abstract string SystemVersion { get; }
        public abstract bool IsBackground { get; }
        public abstract string BackgroundTaskName { get; }
        public abstract int BackgroundTaskId { get; }
    }
}
