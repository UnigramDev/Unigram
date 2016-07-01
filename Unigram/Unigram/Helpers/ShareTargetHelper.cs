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
        ShareOperation shareOperation;
        public string sharedTitle;
        public string sharedDescription;
        public string sharedText;
        public Uri sharedWebLink;



        public ShareTargetHelper(ShareOperation newShareOperation)
        {
            shareOperation = newShareOperation;
            this.GetStuffFromContract();

        }


        // Get stuff from the other app first
        private async void GetStuffFromContract()
        {
            await Task.Factory.StartNew(async () =>
            {
                // Get the properties of the shared package
                this.sharedTitle = this.shareOperation.Data.Properties.Title;
                this.sharedDescription = this.shareOperation.Data.Properties.Description;

                // Now let's get the content! :)
                // Text
                if (this.shareOperation.Data.Contains(StandardDataFormats.Text))
                {
                    try
                    {
                        this.sharedText = await this.shareOperation.Data.GetTextAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("ERROR - ShareTargetHelper - GetText - " + ex);
                    }
                }

                // Web-link
                if (this.shareOperation.Data.Contains(StandardDataFormats.WebLink))
                {
                    try
                    {
                        this.sharedWebLink = await this.shareOperation.Data.GetWebLinkAsync();
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
            this.shareOperation.ReportCompleted();
        }
    }
}
