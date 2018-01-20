namespace watchCode.model
{
    public struct IndexRange
    {
        /// <summary>
        /// the absolute start position
        /// </summary>
        public long Start { get; set; }
        
        /// <summary>
        /// the absolute end position
        /// this includes the last new line character
        /// </summary>
        public long End { get; set; }

        public IndexRange(long start, long end)
        {
            Start = start;
            End = end;
        }

        public static bool operator ==(IndexRange lr1, IndexRange lr2)
        {
            return lr1.Start == lr2.Start && lr1.End == lr2.End;
        }

        public static bool operator !=(IndexRange lr1, IndexRange lr2)
        {
            return !(lr1 == lr2);
        }

        public long GetLength()
        {
            return End - Start;
        }
        
        public override string ToString()
        {
            return Start + "-" + End;
        }
    }
}