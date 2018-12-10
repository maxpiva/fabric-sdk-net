using System.Collections.Generic;
using System.Collections.ObjectModel;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Integration
{
    [TestClass]
    [TestCategory("SDK_INTEGRATION")]
    public class End2endIdemixIT : End2endIT
    {
        private static readonly string FOO_CHANNEL_NAME = "foo";
        private static readonly TestConfig testConfig = TestConfig.Instance;

        internal override IReadOnlyList<SampleOrg> testSampleOrgs { get; set; } = testConfig.GetIntegrationTestsSampleOrgs();
        internal override string CHAIN_CODE_FILEPATH { get; } = "sdkintegration/gocc/sampleIdemix";
        internal override string testName { get; } = "End2endIdemixIT"; //Just print out what test is really running.
        internal override string CHAIN_CODE_NAME { get; } = "idemix_example_go";
        internal override TransactionRequest.Type CHAIN_CODE_LANG { get; } = TransactionRequest.Type.GO_LANG;
        internal override string testUser1 { get; } = "idemixUser";

        internal override void BlockWalker(HFClient client, Channel channel)
        {
            // block walker depends on the state of the chain after go's end2end. Nothing here is language specific so
            // there is no loss in coverage for not doing this.
        }


        [TestMethod]
        public override void Setup()
        {
            sampleStore = new SampleStore(sampleStoreFile);
            SetupUsers(sampleStore);
            RunFabricTest(sampleStore); // just run fabric tests.
        }

        /**
         * Will register and enroll users persisting them to samplestore.
         *
         * @param sampleStore
         * @throws Exception
         */
        public void SetupUsers(SampleStore sampleStore)
        {
            //SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface

            ////////////////////////////
            // get users for all orgs
            foreach (SampleOrg sampleOrg in testSampleOrgs)
            {
                string orgName = sampleOrg.Name;

                SampleUser admin = sampleStore.GetMember(TEST_ADMIN_NAME, orgName);
                sampleOrg.Admin = admin; // The admin of this org.

                sampleOrg.PeerAdmin = sampleStore.GetMember(orgName + "Admin", orgName);
            }

            EnrollIdemixUser(sampleStore);
        }

        public void EnrollIdemixUser(SampleStore sampleStore)
        {
            foreach (SampleOrg sampleOrg in testSampleOrgs)
            {
                HFCAClient ca = sampleOrg.CAClient;

                string orgName = sampleOrg.Name;
                string mspid = sampleOrg.MSPID;
                ca.CryptoSuite = Factory.GetCryptoSuite();

                if (testConfig.IsRunningFabricTLS())
                {
                    //This shows how to get a client TLS certificate from Fabric CA
                    // we will use one client TLS certificate for orderer peers etc.
                    EnrollmentRequest enrollmentRequestTLS = new EnrollmentRequest();
                    enrollmentRequestTLS.AddHost("localhost");
                    enrollmentRequestTLS.Profile = "tls";
                    IEnrollment enroll = ca.Enroll("admin", "adminpw", enrollmentRequestTLS);
                    string tlsCertPEM = enroll.Cert;
                    string tlsKeyPEM = enroll.Key;

                    Properties tlsProperties = new Properties();

                    tlsProperties["clientKeyBytes"] = tlsKeyPEM;
                    tlsProperties["clientCertBytes"] = tlsCertPEM;

                    clientTLSProperties[sampleOrg.Name] = tlsProperties;
                    //Save in samplestore for follow on tests.
                    sampleStore.StoreClientPEMTLCertificate(sampleOrg, tlsCertPEM);
                    sampleStore.StoreClientPEMTLSKey(sampleOrg, tlsKeyPEM);
                }

                HFCAInfo info = ca.Info(); //just check if we connect at all.
                Assert.IsNotNull(info);
                string infoName = info.CAName;
                if (infoName != null && infoName.Length > 0)
                {
                    Assert.AreEqual(ca.CAName, infoName);
                }

                SampleUser admin = sampleStore.GetMember(TEST_ADMIN_NAME, orgName);
                SampleUser idemixUser = sampleStore.GetMember(testUser1, sampleOrg.Name);
                if (!idemixUser.IsRegistered)
                {
                    // users need to be registered AND enrolled
                    RegistrationRequest rr = new RegistrationRequest(idemixUser.Name, "org1.department1");
                    idemixUser.EnrollmentSecret = ca.Register(rr, admin);
                }

                if (!idemixUser.IsEnrolled)
                {
                    idemixUser.Enrollment = ca.Enroll(idemixUser.Name, idemixUser.EnrollmentSecret);
                    idemixUser.MspId = mspid;
                }

                // If running version 1.3, then get Idemix credential
                if (testConfig.IsFabricVersionAtOrAfter("1.3"))
                {
                    string mspID = "idemixMSPID1";
                    if (sampleOrg.Name.Contains("Org2"))
                    {
                        mspID = "idemixMSPID2";
                    }

                    idemixUser.SetIdemixEnrollment(ca.IdemixEnroll(idemixUser.Enrollment, mspID));
                }

                sampleOrg.AddUser(idemixUser);
            }
        }

        internal override Channel ConstructChannel(string name, HFClient client, SampleOrg sampleOrg)
        {
            // override this method since we don't want to construct the channel that's been done.
            // Just get it out of the samplestore!

            client.UserContext = sampleOrg.PeerAdmin;

            return sampleStore.GetChannel(client, name).Initialize();
        }
    }
}