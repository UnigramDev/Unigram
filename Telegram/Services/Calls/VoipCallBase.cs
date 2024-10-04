namespace Telegram.Services.Calls
{
    public abstract partial class VoipCallBase : ServiceBase
    {
        protected VoipCallBase(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public abstract string VideoInputId { get; set; }

        public abstract string AudioInputId { get; set; }

        public abstract string AudioOutputId { get; set; }

        public abstract void Show();

        public abstract void Discard();
    }
}
