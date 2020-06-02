namespace Unigram.Services.Updates
{
    public class UpdateAppVersion
    {
        public CloudUpdate Update { get; set; }

        public UpdateAppVersion(CloudUpdate update)
        {
            Update = update;
        }
    }
}
