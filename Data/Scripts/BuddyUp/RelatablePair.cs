using System;
using System.Collections.Generic;

namespace BuddyUp
{
    public struct RelatablePair
    {
        public RelatablePair(long id1, long id2)
        {
            RelateeId1 = id1;
            RelateeId2 = id2;
        }
        public long RelateeId1;
        public long RelateeId2;

        public static readonly ComparerType Comparer = new ComparerType();
        public class ComparerType : IEqualityComparer<RelatablePair>
        {
            public bool Equals(RelatablePair x, RelatablePair y)
            {
                return (x.RelateeId1 == y.RelateeId1 && x.RelateeId2 == y.RelateeId2) || (x.RelateeId1 == y.RelateeId2 && x.RelateeId2 == y.RelateeId1);
            }
            public int GetHashCode(RelatablePair obj)
            {
                return obj.RelateeId1.GetHashCode() ^ obj.RelateeId2.GetHashCode();
            }
        }
        public override string ToString()
        {
            return $"{RelateeId1} {RelateeId2}";
        }
    }
}
