using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;

namespace Unigram.Helpers
{
    public class ShareTargetHelper
    {
        public ShareOperation ShareOperation { get; private set; }
        public string SharedTitle { get; private set; }
        public string SharedDescription { get; private set; }
        public string SharedText { get; private set; }
        public Uri SharedWebLink { get; private set; }



        public ShareTargetHelper(ShareOperation newShareOperation)
        {
            ShareOperation = newShareOperation;
            this.GetStuffFromContract();

        }


        // Get stuff from the other app first
        private async void GetStuffFromContract()
        {
            await Task.Factory.StartNew(async () =>
            {
                // Get the properties of the shared package
                this.SharedTitle = this.ShareOperation.Data.Properties.Title;
                this.SharedDescription = this.ShareOperation.Data.Properties.Description;

                // Now let's get the content! :)
                // Text
                if (this.ShareOperation.Data.Contains(StandardDataFormats.Text))
                {
                    try
                    {
                        this.SharedText = await this.ShareOperation.Data.GetTextAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("ERROR - ShareTargetHelper - GetText - " + ex);
                    }
                }

                // Web-link
                if (this.ShareOperation.Data.Contains(StandardDataFormats.WebLink))
                {
                    try
                    {
                        this.SharedWebLink = await this.ShareOperation.Data.GetWebLinkAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("ERROR - ShareTargetHelper - GetLink - " + ex);
                    }
                }
            });
        }

        public void CloseShareTarget()
        {
            this.ShareOperation.ReportCompleted();
        }
    }
}
