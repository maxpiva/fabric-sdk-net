using System;

namespace Hyperledger.Fabric.SDK.Exceptions
{
    public class IllegalArgumentException : Exception
    {
        public IllegalArgumentException(string message) : base(message)
        {
        }

        public IllegalArgumentException(string message, Exception parent) : base(message, parent)
        {
        }
        public IllegalArgumentException(Exception parent) : base(parent.Message, parent)
        {
        }
    }
}