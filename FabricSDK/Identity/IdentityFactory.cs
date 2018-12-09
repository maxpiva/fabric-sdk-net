using Hyperledger.Fabric.SDK.Security;

namespace Hyperledger.Fabric.SDK.Identity
{
    public class IdentityFactory
    {
        private IdentityFactory()
        {
            // private constructor for utility class
        }

        public static ISigningIdentity GetSigningIdentity(ICryptoSuite cryptoSuite, IUser user)
        {
            IEnrollment enrollment = user.Enrollment;
            if (enrollment is IdemixEnrollment)
            {
                // Need Idemix signer for this.
                return new IdemixSigningIdentity((IdemixEnrollment) enrollment);
            }
            else
            {
                // for now all others are x509
                return new X509SigningIdentity(cryptoSuite, user);
            }
        }
    }
}