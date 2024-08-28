using System;

namespace Rg.DiffUtils
{
    class Snake
    {
        public int StartX { get; set; }

        public int StartY { get; set; }

        public int EndX { get; set; }

        public int EndY { get; set; }

        public int Size { get; set; }

        public bool IsReverse { get; set; }

        public bool HasAdditionOrRemoval => EndY - StartY != EndX - StartX;

        public bool IsAddition => EndY - StartY > EndX - StartX;

        public int DiagonalSize => Math.Min(EndX - StartX, EndY - StartY);

        public Diagonal ToDiagonal()
        {
            if (HasAdditionOrRemoval)
            {
                if (IsReverse)
                {
                    return new Diagonal(StartX, StartY, DiagonalSize);
                }
                else
                {
                    if (IsAddition)
                        return new Diagonal(StartX, StartY + 1, DiagonalSize);
                    else
                        return new Diagonal(StartX + 1, StartY, DiagonalSize);
                }
            }
            else
            {
                return new Diagonal(StartX, StartY, EndX - StartX);
            }
        }
    }
}
