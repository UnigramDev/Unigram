namespace Rg.DiffUtils
{
    class DiffRange
    {
        public int OldListStart { get; set; }

        public int OldListEnd { get; set; }

        public int NewListStart { get; set; }

        public int NewListEnd { get; set; }

        public int OldSize => OldListEnd - OldListStart;

        public int NewSize => NewListEnd - NewListStart;

        public DiffRange(int oldListStart, int oldListEnd, int newListStart, int newListEnd)
        {
            OldListStart = oldListStart;
            OldListEnd = oldListEnd;
            NewListStart = newListStart;
            NewListEnd = newListEnd;
        }

        public DiffRange()
        {

        }
    }
}
