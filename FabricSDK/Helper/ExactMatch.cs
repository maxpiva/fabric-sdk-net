using System;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class ExactMatch<T> : IEquatable<ExactMatch<T>>
    {
        // use original equals of T and not what's overrident
        private readonly T bas;

        public ExactMatch(T bas)
        {
            this.bas = bas;
        }

        bool IEquatable<ExactMatch<T>>.Equals(ExactMatch<T> other)
        {
            if (other == null)
                return false;
            return bas.Equals(other.bas);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ExactMatch<T>) obj);
        }

        public override int GetHashCode()
        {
            return bas.GetHashCode();
        }
    }
}