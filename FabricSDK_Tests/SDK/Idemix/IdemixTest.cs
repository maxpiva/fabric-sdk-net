using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hyperledger.Fabric.Protos.Idemix;
using Hyperledger.Fabric.SDK.AMCL;
using Hyperledger.Fabric.SDK.AMCL.FP256BN;
using Hyperledger.Fabric.SDK.Idemix;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ECP = Hyperledger.Fabric.SDK.AMCL.FP256BN.ECP;

namespace Hyperledger.Fabric.Tests.SDK.Idemix
{
    [TestClass]
    [TestCategory("SDK")]
    public class IdemixTest
    {
        // Number of tasks to run
        private static readonly int TASKS = 10;

        // How many iterations per task we do
        private static readonly int ITERATIONS = 10;

        [TestMethod]
        public async Task ThreadTestAsync()
        {
            if (!TestConfig.Instance.GetRunIdemixMTTest())
                return;

            // Select attribute names and generate a Idemix Setup
            string[] attributeNames = {"Attr1", "Attr2", "Attr3", "Attr4", "Attr5"};
            IdemixSetup setup = new IdemixSetup(attributeNames);

            // One single task
            IdemixTask taskS = new IdemixTask(setup);
            Assert.IsTrue(await taskS.Call().ConfigureAwait(false));

            // i tasks running at the same time in parallel in different thread pools.
            List<Task> results = new List<Task>();
            for (int i = TASKS; i > 0; i--)
            {
                IdemixTask taskM = new IdemixTask(setup);
                results.Add(Task.Run(async () => { Assert.IsTrue(await taskM.Call().ConfigureAwait(false)); }));
            }

            await Task.WhenAll(results);
        }

        public class IdemixSetup
        {
            public string[] attributeNames;
            public BIG[] attrs;
            public IdemixCredential idemixCredential;
            public IdemixCredRequest idemixCredRequest;
            public BIG issuerNonce;
            public IdemixIssuerKey key;
            public KeyPair revocationKeyPair;
            public BIG sk;
            public WeakBB.KeyPair wbbKeyPair;

            public IdemixSetup(string[] attributeNames)
            {
                // Choose attribute names and create an issuer key pair
                // this.attributeNames = new String[]{"Attribute1", "Attribute2"};
                this.attributeNames = attributeNames;
                key = new IdemixIssuerKey(this.attributeNames);
                RAND rng = IdemixUtils.GetRand();
                // Choose a user secret key and request a credential
                sk = new BIG(rng.RandModOrder());
                issuerNonce = new BIG(rng.RandModOrder());
                idemixCredRequest = new IdemixCredRequest(sk, issuerNonce, key.Ipk); //csr

                // Issue a credential
                attrs = new BIG[this.attributeNames.Length];
                for (int i = 0; i < this.attributeNames.Length; i++)
                {
                    attrs[i] = new BIG(i);
                }

                idemixCredential = new IdemixCredential(key, idemixCredRequest, attrs); //certificate

                wbbKeyPair = WeakBB.WeakBBKeyGen();

                // Generate a revocation key pair
                revocationKeyPair = RevocationAuthority.GenerateLongTermRevocationKey();

                // Check all the generated data
                CheckSetup();
            }


            private void CheckSetup()
            {
                // check that the issuer public key is valid
                Assert.IsTrue(key.Ipk.Check());
                // Test serialization of issuer public key
                Assert.IsTrue(new IdemixIssuerPublicKey(key.Ipk.ToProto()).Check());
                // Test credential request
                Assert.IsTrue(idemixCredRequest.Check(key.Ipk));
                // Test serialization of cred request
                Assert.IsTrue(new IdemixCredRequest(idemixCredRequest.ToProto()).Check(key.Ipk));
                // Test revocation key pair
                Assert.IsNotNull(revocationKeyPair);
            }
        }

        public class IdemixTask
        {
            private int iterations;
            private readonly IdemixSetup setup;

            public IdemixTask(IdemixSetup idemixSetup)
            {
                setup = idemixSetup;
                iterations = ITERATIONS;
            }

            private void Test()
            {
                RAND rng = IdemixUtils.GetRand();
                // WeakBB test
                // Random message to sign
                BIG wbbMessage = rng.RandModOrder();
                // Sign the message with keypair secret key
                ECP wbbSignature = WeakBB.WeakBBSign(setup.wbbKeyPair.Sk, wbbMessage);
                // Check the signature with valid PK and valid message
                Assert.IsTrue(WeakBB.weakBBVerify(setup.wbbKeyPair.Pk, wbbSignature, wbbMessage));
                // Try to check a random message
                Assert.IsFalse(WeakBB.weakBBVerify(setup.wbbKeyPair.Pk, wbbSignature, rng.RandModOrder()));

                // user completes the idemixCredential and checks validity
                Assert.IsTrue(setup.idemixCredential.Verify(setup.sk, setup.key.Ipk));

                // Test serialization of IdemixidemixCredential
                Assert.IsTrue(new IdemixCredential(setup.idemixCredential.ToProto()).Verify(setup.sk, setup.key.Ipk));

                // Create CRI that contains no revocation mechanism
                int epoch = 0;
                BIG[] rhIndex = {new BIG(0)};
                CredentialRevocationInformation cri = RevocationAuthority.CreateCRI(setup.revocationKeyPair, rhIndex, epoch, RevocationAlgorithm.ALG_NO_REVOCATION);

                // Create a new unlinkable pseudonym
                IdemixPseudonym pseudonym = new IdemixPseudonym(setup.sk, setup.key.Ipk); //tcert

                // Test signing no disclosure
                bool[] disclosure = {false, false, false, false, false};
                byte[] msg = {1, 2, 3, 4, 5};
                IdemixSignature signature = new IdemixSignature(setup.idemixCredential, setup.sk, pseudonym, setup.key.Ipk, disclosure, msg, 0, cri);
                Assert.IsNotNull(signature);

                // Test bad disclosure: Disclosure > number of attributes || Disclosure < number of attributes
                bool[] badDisclosure = {false, true};
                bool[] badDisclosure2 = {true, true, true, true, true, true, true};
                try
                {
                    new IdemixSignature(setup.idemixCredential, setup.sk, pseudonym, setup.key.Ipk, badDisclosure, msg, 0, cri);
                    new IdemixSignature(setup.idemixCredential, setup.sk, pseudonym, setup.key.Ipk, badDisclosure2, msg, 0, cri);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException)
                {
                    //ignored
                    /* Do nothing, the expected behaviour is to catch this exception.*/
                }

                // check that the signature is valid
                Assert.IsTrue(signature.Verify(disclosure, setup.key.Ipk, msg, setup.attrs, 0, setup.revocationKeyPair, epoch));

                // Test serialization of IdemixSignature
                Assert.IsTrue(new IdemixSignature(signature.ToProto()).Verify(disclosure, setup.key.Ipk, msg, setup.attrs, 0, setup.revocationKeyPair, epoch));

                // Test signing selective disclosure
                bool[] disclosure2 = {false, true, true, true, false};
                signature = new IdemixSignature(setup.idemixCredential, setup.sk, pseudonym, setup.key.Ipk, disclosure2, msg, 0, cri);
                Assert.IsNotNull(signature);

                // check that the signature is valid
                Assert.IsTrue(signature.Verify(disclosure2, setup.key.Ipk, msg, setup.attrs, 0, setup.revocationKeyPair, epoch));

                // Test signature verification with different disclosure
                Assert.IsFalse(signature.Verify(disclosure, setup.key.Ipk, msg, setup.attrs, 0, setup.revocationKeyPair, epoch));

                // test signature verification with different issuer public key
                Assert.IsFalse(signature.Verify(disclosure2, new IdemixIssuerKey(new [] {"Attr1, Attr2, Attr3, Attr4, Attr5"}).Ipk, msg, setup.attrs, 0, setup.revocationKeyPair, epoch));

                // test signature verification with different message
                byte[] msg2 = {1, 1, 1};
                Assert.IsFalse(signature.Verify(disclosure2, setup.key.Ipk, msg2, setup.attrs, 0, setup.revocationKeyPair, epoch));

                // Sign a message with respect to a pseudonym
                IdemixPseudonymSignature nymsig = new IdemixPseudonymSignature(setup.sk, pseudonym, setup.key.Ipk, msg);
                // check that the pseudonym signature is valid
                Assert.IsTrue(nymsig.Verify(pseudonym.Nym, setup.key.Ipk, msg));

                // Test serialization of IdemixPseudonymSignature
                Assert.IsTrue(new IdemixPseudonymSignature(nymsig.ToProto()).Verify(pseudonym.Nym, setup.key.Ipk, msg));
            }

            public Task<bool> Call()
            {
                return new Task<bool>(() =>
                {
                    for (int i = ITERATIONS; i > 0; --i)
                    {
                        Test();
                    }

                    return true;
                });
            }
        }
    }
}