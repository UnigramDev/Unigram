namespace Rg.DiffUtils
{
    public partial class Diagonal
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int Size { get; set; }

        public int EndX => X + Size;

        public int EndY => Y + Size;

        public Diagonal(int x, int y, int size)
        {
            X = x;
            Y = y;
            Size = size;
        }
    }
}
