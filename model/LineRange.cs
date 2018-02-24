using System;

namespace watchCode.model
{
    public class LineRange
    {
        public int Start { get; set; }
        public int End { get; set; }

        [Obsolete("do not use, only here because of json deserialization")]
        public LineRange()
        {
        }

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

        public LineRange Clone()
        {
            return new LineRange()
            {
                End = this.End,
                Start = this.Start
            };
        }

        public static bool operator ==(LineRange lr1, LineRange lr2)
        {
            if (ReferenceEquals(lr1, lr2))
            {
                return true;
            }

            if (ReferenceEquals(lr1, null))
            {
                return false;
            }
            if (ReferenceEquals(lr2, null))
            {
                return false;
            }

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

        /// <summary>
        /// tries to return a short representation of the rang
        /// </summary>
        /// <returns></returns>
        public string ToShortString()
        {
            if (Start == End) return Start.ToString();

            return Start + "-" + End;
        }

        public static LineRange ReverseLineRange(LineRange lineRange, int linesCount)
        {
            return new LineRange(
                linesCount - lineRange.End,
                linesCount - lineRange.Start
            );
        } 
    }
}
