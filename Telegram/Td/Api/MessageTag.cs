using Telegram.Navigation;

namespace Telegram.Td.Api
{
    public partial class MessageTag : BindableBase
    {
        public MessageTag(SavedMessagesTag tag)
        {
            _count = tag.Count;
            _label = tag.Label;

            Tag = tag.Tag;
        }

        private int _count;

        /// <summary>
        /// Number of times the tag was used; may be 0 if the tag has non-empty label.
        /// </summary>
        public int Count
        {
            get => _count;
            set => Set(ref _count, value);
        }

        private string _label;

        /// <summary>
        /// Label of the tag; 0-12 characters.
        /// </summary>
        public string Label
        {
            get => _label;
            set => Set(ref _label, value);
        }

        /// <summary>
        /// The tag.
        /// </summary>
        public ReactionType Tag { get; }
    }
}
