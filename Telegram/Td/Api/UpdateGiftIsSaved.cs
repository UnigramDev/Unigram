namespace Telegram.Td.Api
{
    public partial class UpdateGiftIsSold
    {
        public string ReceivedGiftId { get; set; }

        public UpdateGiftIsSold(string receivedGiftId)
        {
            ReceivedGiftId = receivedGiftId;
        }
    }
}
