namespace Rg.DiffUtils
{
    public partial class DiffItem<T>
    {
        public int OldSeqIndex { get; }

        public int NewSeqIndex { get; }

        public T OldValue { get; }

        public T NewValue { get; }

        public DiffItem(int oldSeqIndex, int newSeqIndex, T oldValue, T newValue)
        {
            OldSeqIndex = oldSeqIndex;
            NewSeqIndex = newSeqIndex;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
