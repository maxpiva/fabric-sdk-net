using System;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Identity
{
    public class X509Identity : IIdentity
    {
        protected readonly IUser user;

        public X509Identity(IUser user)
        {
            if (user == null)
                throw new ArgumentException("User is null");
            if (user.Enrollment == null)
                throw new ArgumentException("user.getEnrollment() is null");
            if (user.Enrollment.Cert == null)
                throw new ArgumentException("user.getEnrollment().getCert() is null");
            this.user = user;
        }


        public SerializedIdentity CreateSerializedIdentity()
        {
            return ProtoUtils.CreateSerializedIdentity(user);
        }
    }
}