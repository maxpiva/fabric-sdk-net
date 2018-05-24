using System;
using System.Collections.Generic;
using System.Text;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hyperledger.Fabric.SDK.Security
{
    public class KeyPair
    {
        public AsymmetricAlgorithm PublicKey { get; set; }
        public AsymmetricAlgorithm PrivateKey { get; set; }
    }
}
