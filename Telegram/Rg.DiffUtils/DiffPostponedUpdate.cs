namespace Rg.DiffUtils
{
    class DiffPostponedUpdate
    {
        public int PosInOwnerList { get; }

        public int CurrentPos { get; set; }

        public bool Removal { get; }

        public DiffPostponedUpdate(int posInOwnerList, int currentPos, bool removal)
        {
            PosInOwnerList = posInOwnerList;
            CurrentPos = currentPos;
            Removal = removal;
        }
    }
}
