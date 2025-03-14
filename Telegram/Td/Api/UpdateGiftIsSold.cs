namespace Telegram.Td.Api
{
    public partial class UpdateGiftUpgraded
    {
        public string ReceivedGiftId { get; set; }

        public ReceivedGift Gift { get; set; }

        public UpdateGiftUpgraded(string receivedGiftId, ReceivedGift gift)
        {
            ReceivedGiftId = receivedGiftId;
            Gift = gift;
        }
    }
}
