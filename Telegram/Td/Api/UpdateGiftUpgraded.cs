namespace Telegram.Td.Api
{
    public partial class UpdateGiftIsSaved
    {
        public string ReceivedGiftId { get; set; }

        public bool IsSaved { get; set; }

        public UpdateGiftIsSaved(string receivedGiftId, bool isSaved)
        {
            ReceivedGiftId = receivedGiftId;
            IsSaved = isSaved;
        }
    }
}
