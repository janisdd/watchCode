namespace watchCode.model
{
    public struct LineRange
    {
        public int Start { get; set; }
        public int End { get; set; }

        public LineRange(int start, int end)
        {
            Start = start;
            End = end;
        }

        public LineRange(int startAndEnd)
        {
            Start = startAndEnd;
            End = startAndEnd;
        }

        public static bool operator ==(LineRange lr1, LineRange lr2)
        {
            return lr1.Start == lr2.Start && lr1.End == lr2.End;
        }

        public static bool operator !=(LineRange lr1, LineRange lr2)
        {
            return !(lr1 == lr2);
        }

        public int GetLength()
        {
            return End - Start + 1; //e.g. 2-5 = 4 lines
        }
        
        public override string ToString()
        {
            return Start + "-" + End;
        }
    }
}