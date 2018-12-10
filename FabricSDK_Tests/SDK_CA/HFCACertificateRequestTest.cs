using System;
using System.IO;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.Integration;
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK_CA
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class HFCACertificateRequestTest
    {
        private static readonly string TEST_ADMIN_NAME = "admin";
        private static readonly string TEST_ADMIN_ORG = "org1";

        private static CryptoPrimitives crypto;
        private SampleUser admin;

        private SampleStore sampleStore;
        private readonly string sampleStoreFile = Path.Combine(Path.GetTempPath(), "HFCSampletest.properties");

        [ClassInitialize]
        public static void setupBeforeClass(TestContext context)
        {
            try
            {
                crypto = new CryptoPrimitives();
                crypto.Init();
            }
            catch (System.Exception e)
            {
                throw new ArgumentException("HFCAAffiliationTest.setupBeforeClass failed!", e);
            }
        }

        [TestCleanup]
        public void CleanUp()
        {
            if (File.Exists(sampleStoreFile))
            {
                // For testing start fresh
                File.Delete(sampleStoreFile);
            }
        }

        [TestInitialize]
        public void Setup()
        {
            if (File.Exists(sampleStoreFile))
            {
                // For testing start fresh
                File.Delete(sampleStoreFile);
            }

            sampleStore = new SampleStore(sampleStoreFile);

            // SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface
            admin = sampleStore.GetMember(TEST_ADMIN_NAME, TEST_ADMIN_ORG);
        }

        [TestMethod]
        public void TestHFCACertificateNewInstance()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            HFCACertificateRequest certReq = client.NewHFCACertificateRequest();
            Assert.IsNotNull(certReq);
            Assert.AreEqual(typeof(HFCACertificateRequest).Name, certReq.GetType().Name);
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(HFCACertificateException), "Error while getting certificates")]
        public void GetCertificateNoServerResponse()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;

            HFCACertificateRequest certReq = client.NewHFCACertificateRequest();
            client.GetHFCACertificates(admin, certReq);
        }
    }
}