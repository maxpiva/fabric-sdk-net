/*
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *      http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.Integration;
using Hyperledger.Fabric.Tests.SDK.TestUtils;
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Attribute = Hyperledger.Fabric_CA.SDK.Attribute;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using X509Extension = System.Security.Cryptography.X509Certificates.X509Extension;

namespace Hyperledger.Fabric.Tests.SDK_CA.Integration
{
    [TestClass]
    [TestCategory("SDK_CA_INTEGRATION")]
    public class HFCAClientIT
    {
        private static readonly string TEST_ADMIN_NAME = "admin";
        private static readonly string TEST_ADMIN_PW = "adminpw";
        private static readonly string TEST_ADMIN_ORG = "org1";
        private static readonly string TEST_USER1_ORG = "Org2";
        private static readonly string TEST_USER1_AFFILIATION = "org1.department1";
        private static readonly string TEST_WITH_INTEGRATION_ORG = "peerOrg1";
        private static readonly string TEST_WITH_INTEGRATION_ORG2 = "peerOrg2";

        private static ICryptoSuite crypto;

        // Keeps track of how many test users we've created
        private static int userCount = 0;

        // Common prefix for all test users (the suffix will be the current user count)
        // Note that we include the time value so that these tests can be executed repeatedly
        // without needing to restart the CA (because you cannot register a username more than once!)
        private static readonly string userNamePrefix = "user" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000 + "_";

        private static readonly TestConfig testConfig = TestConfig.Instance;

        private static readonly Regex compile = new Regex("^-----BEGIN CERTIFICATE-----$" + "(.*?)" + "\n-----END CERTIFICATE-----\n", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
        private SampleUser admin;
        private HFCAClient client;

        private SampleStore sampleStore;

        [ClassInitialize]
        public static void Init(TestContext context)
        {
            Util.COut("\n\n\nRUNNING: HFCAClientEnrollIT.\n");

            TestUtils.ResetConfig();

            crypto = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
        }

        [TestInitialize]
        public void Setup()
        {
            string sampleStoreFile = Path.Combine(Path.GetTempPath(), "HFCSampletest.properties");
            if (File.Exists(sampleStoreFile))
            {
                // For testing start fresh
                File.Delete(sampleStoreFile);
            }

            sampleStore = new SampleStore(sampleStoreFile);

            client = HFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CAProperties);
            client.CryptoSuite = crypto;

            // SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface
            admin = sampleStore.GetMember(TEST_ADMIN_NAME, TEST_ADMIN_ORG);
            if (!admin.IsEnrolled)
            {
                // Preregistered admin only needs to be enrolled with Fabric CA.
                admin.Enrollment = client.Enroll(admin.Name, TEST_ADMIN_PW);
            }
        }

        // Tests attributes
        [TestMethod]
        public void TestRegisterAttributes()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            SampleUser user = new SampleUser("mrAttributes", TEST_ADMIN_ORG, sampleStore);

            RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
            string password = "mrAttributespassword";
            rr.Secret = password;

            rr.AddAttribute(new Attribute("testattr1", "mrAttributesValue1"));
            rr.AddAttribute(new Attribute("testattr2", "mrAttributesValue2"));
            rr.AddAttribute(new Attribute("testattrDEFAULTATTR", "mrAttributesValueDEFAULTATTR", true));
            user.EnrollmentSecret = client.Register(rr, admin);
            if (!user.EnrollmentSecret.Equals(password))
            {
                Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
            }

            EnrollmentRequest req = new EnrollmentRequest();
            req.AddAttrReq("testattr2").SetOptional(false);

            user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret, req);

            IEnrollment enrollment = user.Enrollment;
            string cert = enrollment.Cert;
            string certdec = GetStringCert(cert);

            Assert.IsTrue(certdec.Contains("\"testattr2\":\"mrAttributesValue2\""), $"Missing testattr2 in certficate decoded: {certdec}");
            //Since request had specific attributes don't expect defaults.
            Assert.IsFalse(certdec.Contains("\"testattrDEFAULTATTR\"") || certdec.Contains("\"mrAttributesValueDEFAULTATTR\""), $"Contains testattrDEFAULTATTR in certificate decoded: {certdec}");
            Assert.IsFalse(certdec.Contains("\"testattr1\"") || certdec.Contains("\"mrAttributesValue1\""), $"Contains testattr1 in certificate decoded: {certdec}");
        }

        /**
         * Test that we get default attributes.
         *
         * @throws Exception
         */
        [TestMethod]
        public void TestRegisterAttributesDefault()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            SampleUser user = new SampleUser("mrAttributesDefault", TEST_ADMIN_ORG, sampleStore);

            RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
            string password = "mrAttributespassword";
            rr.Secret = password;

            rr.AddAttribute(new Attribute("testattr1", "mrAttributesValue1"));
            rr.AddAttribute(new Attribute("testattr2", "mrAttributesValue2"));
            rr.AddAttribute(new Attribute("testattrDEFAULTATTR", "mrAttributesValueDEFAULTATTR", true));
            user.EnrollmentSecret = client.Register(rr, admin);
            if (!user.EnrollmentSecret.Equals(password))
            {
                Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
            }

            user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret);

            IEnrollment enrollment = user.Enrollment;
            string cert = enrollment.Cert;

            string certdec = GetStringCert(cert);

            Assert.IsTrue(certdec.Contains("\"testattrDEFAULTATTR\":\"mrAttributesValueDEFAULTATTR\""), $"Missing testattrDEFAULTATTR in certficate decoded:{certdec}");
            //Since request and no attribute requests at all defaults should be in certificate.

            Assert.IsFalse(certdec.Contains("\"testattr1\"") || certdec.Contains("\"mrAttributesValue1\""), $"Contains testattr1 in certificate decoded: {certdec}");
            Assert.IsFalse(certdec.Contains("\"testattr2\"") || certdec.Contains("\"mrAttributesValue2\""), $"Contains testattr2 in certificate decoded: {certdec}");
        }

        /**
         * Test that we get no attributes.
         *
         * @throws Exception
         */
        [TestMethod]
        public void TestRegisterAttributesNONE()
        {
            SampleUser user = new SampleUser("mrAttributesNone", TEST_ADMIN_ORG, sampleStore);

            RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
            string password = "mrAttributespassword";
            rr.Secret = password;

            rr.AddAttribute(new Attribute("testattr1", "mrAttributesValue1"));
            rr.AddAttribute(new Attribute("testattr2", "mrAttributesValue2"));
            rr.AddAttribute(new Attribute("testattrDEFAULTATTR", "mrAttributesValueDEFAULTATTR", true));
            user.EnrollmentSecret = client.Register(rr, admin);
            if (!user.EnrollmentSecret.Equals(password))
            {
                Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
            }

            EnrollmentRequest req = new EnrollmentRequest();
            req.AddAttrReq(); // empty ensure no attributes.

            user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret, req);

            IEnrollment enrollment = user.Enrollment;
            string cert = enrollment.Cert;

            string certdec = GetStringCert(cert);

            Assert.IsFalse(certdec.Contains("\"testattrDEFAULTATTR\"") || certdec.Contains("\"mrAttributesValueDEFAULTATTR\""), $"Contains testattrDEFAULTATTR in certificate decoded: {certdec}");
            Assert.IsFalse(certdec.Contains("\"testattr1\"") || certdec.Contains("\"mrAttributesValue1\""), $"Contains testattr1 in certificate decoded: {certdec}");
            Assert.IsFalse(certdec.Contains("\"testattr2\"") || certdec.Contains("\"mrAttributesValue2\""), $"Contains testattr2 in certificate decoded: {certdec}");
        }

        private static string GetStringCert(string pemFormat)
        {
            string ret = null;

            Match matcher = compile.Match(pemFormat);
            if (matcher.Success)
            {
                string base64part = matcher.Groups[1].Value.Replace("\n", "");
                return Convert.FromBase64String(base64part).ToUTF8String();
            }

            Assert.Fail($"Certificate failed to match expected pattern. Certificate:\n {pemFormat}");
            return ret;
        }

        // Tests re-enrolling a user that has had an enrollment revoked
        [TestMethod]
        public void TestReenrollAndRevoke()
        {
            SampleUser user = GetTestUser(TEST_ADMIN_ORG);

            if (!user.IsRegistered)
            {
                // users need to be registered AND enrolled
                RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
                string password = "testReenrollAndRevoke";
                rr.Secret = password;
                user.EnrollmentSecret = client.Register(rr, admin);
                if (!user.EnrollmentSecret.Equals(password))
                {
                    Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
                }
            }

            if (!user.IsEnrolled)
            {
                user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret);
            }

            SleepALittle();

            // get another enrollment
            EnrollmentRequest req = new EnrollmentRequest(HFCAClient.DEFAULT_PROFILE_NAME, "label 1", null);
            req.AddHost("example1.ibm.com");
            req.AddHost("example2.ibm.com");
            IEnrollment tmpEnroll = client.Reenroll(user, req);

            // verify
            string cert = tmpEnroll.Cert;
            VerifyOptions(cert, req);

            SleepALittle();

            // revoke one enrollment of this user
            client.Revoke(admin, tmpEnroll, "remove user 2");

            // trying to reenroll should be ok (revocation above is only for a particular enrollment of this user)
            client.Reenroll(user);
        }

        // Tests attempting to re-enroll a revoked user
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "Failed to re-enroll user")]
        public void TestUserRevoke()
        {
//        Calendar calendar = Calendar.Instance; // gets a calendar using the default time zone and locale.
            //      Date revokedTinyBitAgoTime = calendar.Time; //avoid any clock skewing.

            SampleUser user = GetTestUser(TEST_USER1_ORG);

            if (!user.IsRegistered)
            {
                RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
                string password = "testUserRevoke";
                rr.Secret = password;
                rr.AddAttribute(new Attribute("user.role", "department lead"));
                rr.AddAttribute(new Attribute(HFCAClient.HFCA_ATTRIBUTE_HFREVOKER, "true"));
                user.EnrollmentSecret = client.Register(rr, admin); // Admin can register other users.
                if (!user.EnrollmentSecret.Equals(password))
                {
                    Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
                }
            }

            if (!user.IsEnrolled)
            {
                EnrollmentRequest req = new EnrollmentRequest(HFCAClient.DEFAULT_PROFILE_NAME, "label 2", null);
                req.AddHost("example3.ibm.com");
                user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret, req);

                // verify
                string cert = user.Enrollment.Cert;
                VerifyOptions(cert, req);
            }

            int startedWithRevokes = -1;

            if (!testConfig.IsRunningAgainstFabric10())
            {
                Thread.Sleep(1000); //prevent clock skewing. make sure we request started with revokes.
                startedWithRevokes = GetRevokes(null).Count; //one more after we do this revoke.
                Thread.Sleep(1000); //prevent clock skewing. make sure we request started with revokes.
            }

            // revoke all enrollment of this user
            client.Revoke(admin, user.Name, "revoke user 3");
            if (!testConfig.IsRunningAgainstFabric10())
            {
                int newRevokes = GetRevokes(null).Count;

                Assert.AreEqual(startedWithRevokes + 1, newRevokes, $"Expected one more revocation {startedWithRevokes + 1}, but got {newRevokes}");

                // see if we can get right number of revokes that we started with by specifying the time: revokedTinyBitAgoTime
                // TODO: Investigate clock scew
//            int revokestinybitago = getRevokes(revokedTinyBitAgoTime).length; //Should be same number when test case was started.
//            Assert.AreEqual(format("Expected same revocations %d, but got %d", startedWithRevokes, revokestinybitago), startedWithRevokes, revokestinybitago);
            }

            // trying to reenroll the revoked user should fail with an EnrollmentException
            client.Reenroll(user);
        }

        // Tests revoking a certificate
        [TestMethod]
        public void TestCertificateRevoke()
        {
            SampleUser user = GetTestUser(TEST_USER1_ORG);

            if (!user.IsRegistered)
            {
                RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
                string password = "testUserRevoke";
                rr.Secret = password;
                rr.AddAttribute(new Attribute("user.role", "department lead"));
                rr.AddAttribute(new Attribute(HFCAClient.HFCA_ATTRIBUTE_HFREVOKER, "true"));
                user.EnrollmentSecret = client.Register(rr, admin); // Admin can register other users.
                if (!user.EnrollmentSecret.Equals(password))
                {
                    Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
                }
            }

            if (!user.IsEnrolled)
            {
                EnrollmentRequest req = new EnrollmentRequest(HFCAClient.DEFAULT_PROFILE_NAME, "label 2", null);
                req.AddHost("example3.ibm.com");
                user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret, req);
            }

            // verify
            string cert = user.Enrollment.Cert;

            X509Certificate2 certificat = ((CryptoPrimitives) crypto).BytesToCertificate(user.Enrollment.Cert.ToBytes());
            X509Certificate ncert = DotNetUtilities.FromX509Certificate(certificat);

            // get its serial number
            string serial = ncert.SerialNumber.ToByteArray().ToHexString();

            // get its aki
            // 2.5.29.35 : AuthorityKeyIdentifier


            Asn1OctetString akiOc = ncert.GetExtensionValue(X509Extensions.AuthorityKeyIdentifier.Id);
            string aki = AuthorityKeyIdentifier.GetInstance(akiOc.GetOctets()).GetKeyIdentifier().ToHexString();


            int startedWithRevokes = -1;

            if (!testConfig.IsRunningAgainstFabric10())
            {
                Thread.Sleep(1000); //prevent clock skewing. make sure we request started with revokes.
                startedWithRevokes = GetRevokes(null).Count; //one more after we do this revoke.
                Thread.Sleep(1000); //prevent clock skewing. make sure we request started with revokes.
            }

            // revoke all enrollment of this user
            client.Revoke(admin, serial, aki, "revoke certificate");
            if (!testConfig.IsRunningAgainstFabric10())
            {
                int newRevokes = GetRevokes(null).Count;

                Assert.AreEqual(startedWithRevokes + 1, newRevokes, $"Expected one more revocation {startedWithRevokes + 1}, but got {newRevokes}");
            }
        }

        // Tests attempting to revoke a user with Null reason
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "Failed to re-enroll user")]
        public void TestUserRevokeNullReason()
        {
//        Calendar calendar = Calendar.Instance; // gets a calendar using the default time zone and locale.
            //      calendar.add(Calendar.SECOND, -1);
            //    Date revokedTinyBitAgoTime = calendar.Time; //avoid any clock skewing.

            SampleUser user = GetTestUser(TEST_USER1_ORG);

            if (!user.IsRegistered)
            {
                RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
                string password = "testUserRevoke";
                rr.Secret = password;
                rr.AddAttribute(new Attribute("user.role", "department lead"));
                rr.AddAttribute(new Attribute(HFCAClient.HFCA_ATTRIBUTE_HFREVOKER, "true"));
                user.EnrollmentSecret = client.Register(rr, admin); // Admin can register other users.
                if (!user.EnrollmentSecret.Equals(password))
                {
                    Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
                }
            }

            SleepALittle();

            if (!user.IsEnrolled)
            {
                EnrollmentRequest req = new EnrollmentRequest(HFCAClient.DEFAULT_PROFILE_NAME, "label 2", null);
                req.AddHost("example3.ibm.com");
                user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret, req);

                // verify
                string cert = user.Enrollment.Cert;
                VerifyOptions(cert, req);
            }

            SleepALittle();

            int startedWithRevokes = -1;

            if (!testConfig.IsRunningAgainstFabric10())
            {
                startedWithRevokes = GetRevokes(null).Count; //one more after we do this revoke.
            }

            // revoke all enrollment of this user
            client.Revoke(admin, user.Name, null);
            if (!testConfig.IsRunningAgainstFabric10())
            {
                int newRevokes = GetRevokes(null).Count;

                Assert.AreEqual(startedWithRevokes + 1, newRevokes, $"Expected one more revocation {startedWithRevokes + 1}, but got {newRevokes}");
            }

            // trying to reenroll the revoked user should fail with an EnrollmentException
            client.Reenroll(user);
        }

        // Tests revoking a user with genCRL using the revoke API
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "Failed to re-enroll user")]
        public void TestUserRevokeGenCRL()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }


            //Calendar calendar = Calendar.Instance; // gets a calendar using the default time zone and locale.
            //calendar.add(Calendar.SECOND, -1);
            //Date revokedTinyBitAgoTime = calendar.Time; //avoid any clock skewing.

            SampleUser user1 = GetTestUser(TEST_USER1_ORG);
            SampleUser user2 = GetTestUser(TEST_USER1_ORG);

            SampleUser[] users = new SampleUser[] {user1, user2};

            foreach (SampleUser user in users)
            {
                if (!user.IsRegistered)
                {
                    RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
                    string password = "testUserRevoke";
                    rr.Secret = password;
                    rr.AddAttribute(new Attribute("user.role", "department lead"));
                    rr.AddAttribute(new Attribute(HFCAClient.HFCA_ATTRIBUTE_HFREVOKER, "true"));
                    user.EnrollmentSecret = client.Register(rr, admin); // Admin can register other users.
                    if (!user.EnrollmentSecret.Equals(password))
                    {
                        Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
                    }
                }

                SleepALittle();

                if (!user.IsEnrolled)
                {
                    EnrollmentRequest req = new EnrollmentRequest(HFCAClient.DEFAULT_PROFILE_NAME, "label 2", null);
                    req.AddHost("example3.ibm.com");
                    user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret, req);

                    // verify
                    string cert = user.Enrollment.Cert;
                    VerifyOptions(cert, req);
                }
            }

            SleepALittle();

            int startedWithRevokes = -1;

            startedWithRevokes = GetRevokes(null).Count; //one more after we do this revoke.

            // revoke all enrollment of this user and request back a CRL
            string crl = client.Revoke(admin, user1.Name, null, true);
            Assert.IsNotNull("Failed to get CRL using the Revoke API", crl);

            int newRevokes = GetRevokes(null).Count;

            Assert.AreEqual(startedWithRevokes + 1, newRevokes, $"Expected one more revocation {startedWithRevokes + 1}, but got {newRevokes}");

            int crlLength = ParseCRL(crl).Count;

            Assert.AreEqual(newRevokes, crlLength, $"The number of revokes {newRevokes} does not equal the number of revoked certificates ({crlLength}) in crl");

            // trying to reenroll the revoked user should fail with an EnrollmentException
            client.Reenroll(user1);

            string crl2 = client.Revoke(admin, user2.Name, null, false);
            Assert.AreEqual("CRL not requested, CRL should be empty", "", crl2);
        }

        private List<CrlEntry> GetRevokes(DateTime? r)
        {
            string crl = client.GenerateCRL(admin, r, null, null, null);

            return ParseCRL(crl);
        }

        private List<CrlEntry> ParseCRL(string crl)
        {
            byte[] decode = Convert.FromBase64String(crl);
            PemReader pem = new PemReader(new StreamReader(new MemoryStream(decode)));
            X509Crl holder = (X509Crl) pem.ReadObject();
            return holder.GetRevokedCertificates().Cast<CrlEntry>().ToList();
        }

        // Tests getting an identity
        [TestMethod]
        public void TestCreateAndGetIdentity()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAIdentity ident = GetIdentityReq("testuser1", HFCAClient.HFCA_TYPE_PEER);
            ident.Create(admin);

            HFCAIdentity identGet = client.NewHFCAIdentity(ident.EnrollmentId);
            identGet.Read(admin);
            Assert.AreEqual(ident.EnrollmentId, identGet.EnrollmentId, "Incorrect response for id");
            Assert.AreEqual(ident.Type, identGet.Type, "Incorrect response for type");
            Assert.AreEqual(ident.Affiliation, identGet.Affiliation, "Incorrect response for affiliation");
            Assert.AreEqual(ident.MaxEnrollments, identGet.MaxEnrollments, "Incorrect response for max enrollments");

            List<Attribute> attrs = identGet.Attributes;
            bool found = false;
            foreach (Attribute attr in attrs)
            {
                if (attr.Name.Equals("testattr1"))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Assert.Fail("Incorrect response for attribute");
            }
        }

        // Tests getting an identity that does not exist
        [TestMethod]
        public void TestGetIdentityNotExist()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            client.StatusCode = 405;

            HFCAIdentity ident = client.NewHFCAIdentity("fakeUser");
            int statusCode = ident.Read(admin);
            if (statusCode != 404)
            {
                Assert.Fail("Incorrect status code return for an identity that is not found, should have returned 404 and not thrown an excpetion");
            }

            client.StatusCode = 400;
        }

        // Tests getting all identities for a caller
        [TestMethod]
        public void TestGetAllIdentity()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAIdentity ident = GetIdentityReq("testuser2", HFCAClient.HFCA_TYPE_CLIENT);
            ident.Create(admin);

            List<HFCAIdentity> foundIdentities = client.GetHFCAIdentities(admin);
            string[] expectedIdenities = new string[] {"testuser2", "admin"};
            int found = 0;

            foreach (HFCAIdentity id in foundIdentities)
            {
                foreach (string name in expectedIdenities)
                {
                    if (id.EnrollmentId.Equals(name))
                    {
                        found++;
                    }
                }
            }

            if (found != 2)
            {
                Assert.Fail("Failed to get the correct number of identities");
            }
        }

        // Tests modifying an identity
        [TestMethod]
        public void TestModifyIdentity()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAIdentity ident = GetIdentityReq("testuser3", HFCAClient.HFCA_TYPE_ORDERER);
            ident.Create(admin);
            Assert.AreEqual("orderer", ident.Type, "Incorrect response for type");
            Assert.AreNotEqual(ident.MaxEnrollments, 5, "Incorrect value for max enrollments");

            ident.MaxEnrollments = 5;
            ident.Update(admin);
            Assert.AreEqual(ident.MaxEnrollments, 5, "Incorrect value for max enrollments");

            ident.MaxEnrollments = 100;
            ident.Read(admin);
            Assert.AreEqual(5, ident.MaxEnrollments, "Incorrect value for max enrollments");
        }

        // Tests deleting an identity
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IdentityException), "Failed to get User")]
        public void TestDeleteIdentity()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }


            SampleUser user = new SampleUser("testuser4", TEST_ADMIN_ORG, sampleStore);

            HFCAIdentity ident = client.NewHFCAIdentity(user.Name);

            ident.Create(admin);
            ident.Delete(admin);

            ident.Read(admin);
        }

        // Tests deleting an identity and making sure it can't update after deletion
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IdentityException), "Identity has been deleted")]
        public void TestDeleteIdentityFailUpdate()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }


            HFCAIdentity ident = client.NewHFCAIdentity("deletedUser");

            ident.Create(admin);
            ident.Delete(admin);

            ident.Update(admin);
        }

        // Tests deleting an identity and making sure it can't delete again
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IdentityException), "Identity has been deleted")]
        public void TestDeleteIdentityFailSecondDelete()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }


            HFCAIdentity ident = client.NewHFCAIdentity("deletedUser2");

            ident.Create(admin);
            ident.Delete(admin);

            ident.Delete(admin);
        }

        // Tests deleting an identity on CA that does not allow identity removal
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "Identity removal is disabled")]
        public void TestDeleteIdentityNotAllowed()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                throw new System.Exception("Identity removal is disabled");
                // needs v1.1
            }

            SampleUser user = new SampleUser("testuser5", "org2", sampleStore);

            HFCAClient client2 = HFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG2).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG2).CAProperties);
            client2.CryptoSuite = crypto;

            // SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface
            SampleUser admin2 = sampleStore.GetMember(TEST_ADMIN_NAME, "org2");
            if (!admin2.IsEnrolled)
            {
                // Preregistered admin only needs to be enrolled with Fabric CA.
                admin2.Enrollment = client2.Enroll(admin.Name, TEST_ADMIN_PW);
            }

            HFCAIdentity ident = client2.NewHFCAIdentity(user.Name);

            ident.Create(admin2);
            ident.Delete(admin2);
        }

        // Tests getting an affiliation
        [TestMethod]
        public void TestGetAffiliation()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAAffiliation aff = client.NewHFCAAffiliation("org2");
            int resp = aff.Read(admin);

            Assert.AreEqual("org2", aff.Name, "Incorrect response for affiliation name");
            Assert.AreEqual("org2.department1", aff.GetChild("department1").Name, "Incorrect response for child affiliation name");
            Assert.AreEqual(200, resp, "Incorrect status code");
        }

        // Tests getting all affiliation
        [TestMethod]
        public void TestGetAllAffiliation()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAAffiliation resp = client.GetHFCAAffiliations(admin);

            List<string> expectedFirstLevelAffiliations = new List<string> {"org2", "org1"};
            int found = 0;
            foreach (HFCAAffiliation aff in resp.Children)
            {
                foreach (string element in expectedFirstLevelAffiliations.ToList())
                {
                    if (aff.Name.Equals(element))
                    {
                        expectedFirstLevelAffiliations.Remove(element);
                    }
                }
            }

            if (expectedFirstLevelAffiliations.Count != 0)
            {
                Assert.Fail($"Failed to. the correct of affiliations, affiliations not returned: {expectedFirstLevelAffiliations}");
            }

            List<string> expectedSecondLevelAffiliations = new List<string> {"org2.department1", "org1.department1", "org1.department2"};
            foreach (HFCAAffiliation aff in resp.Children)
            {
                foreach (HFCAAffiliation aff2 in aff.Children)
                {
                    if (expectedSecondLevelAffiliations.Contains(aff2.Name))
                        expectedSecondLevelAffiliations.Remove(aff2.Name);
                }
            }

            if (expectedSecondLevelAffiliations.Count != 0)
            {
                Assert.Fail($"Failed to. the correct child affiliations, affiliations not returned: {expectedSecondLevelAffiliations}");
            }
        }

        // Tests adding an affiliation
        [TestMethod]
        public void TestCreateAffiliation()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAAffiliation aff = client.NewHFCAAffiliation("org3");
            HFCAAffiliation.HFCAAffiliationResp resp = aff.Create(admin);

            Assert.AreEqual(201, resp.StatusCode, "Incorrect status code");
            Assert.AreEqual("org3", aff.Name, "Incorrect response for id");

            List<HFCAAffiliation> children = aff.Children;
            Assert.AreEqual(0, children.Count, "Should have no children");
        }

        // Tests updating an affiliation
        [TestMethod]
        public void TestUpdateAffiliation()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAAffiliation aff = client.NewHFCAAffiliation("org4");
            aff.Create(admin);

            HFCAIdentity ident = client.NewHFCAIdentity("testuser_org4");
            ident.Affiliation = aff.Name;
            ident.Create(admin);

            HFCAAffiliation aff2 = client.NewHFCAAffiliation("org4.dept1");
            aff2.Create(admin);

            HFCAIdentity ident2 = client.NewHFCAIdentity("testuser_org4.dept1");
            ident2.Affiliation = "org4.dept1";
            ident2.Create(admin);

            HFCAAffiliation aff3 = client.NewHFCAAffiliation("org4.dept1.team1");
            aff3.Create(admin);

            HFCAIdentity ident3 = client.NewHFCAIdentity("testuser_org4.dept1.team1");
            ident3.Affiliation = "org4.dept1.team1";
            ident3.Create(admin);

            aff.UpdateName = "org5";
            // Set force option to true, since their identities associated with affiliations
            // that are getting updated
            HFCAAffiliation.HFCAAffiliationResp resp = aff.Update(admin, true);

            int found = 0;
            int idCount = 0;
            // Should contain the affiliations affected by the update request
            HFCAAffiliation child = aff.GetChild("dept1");
            Assert.IsNotNull(child);
            Assert.AreEqual("org5.dept1", child.Name, "Failed to. correct child affiliation");
            foreach (HFCAIdentity id in child.Identities)
            {
                if (id.EnrollmentId.Equals("testuser_org4.dept1"))
                {
                    idCount++;
                }
            }

            HFCAAffiliation child2 = child.GetChild("team1");
            Assert.IsNotNull(child2);
            Assert.AreEqual("org5.dept1.team1", child2.Name, "Failed to. correct child affiliation");
            foreach (HFCAIdentity id in child2.Identities)
            {
                if (id.EnrollmentId.Equals("testuser_org4.dept1.team1"))
                {
                    idCount++;
                }
            }

            foreach (HFCAIdentity id in aff.Identities)
            {
                if (id.EnrollmentId.Equals("testuser_org4"))
                {
                    idCount++;
                }
            }

            if (idCount != 3)
            {
                Assert.Fail("Incorrect number of ids returned");
            }

            Assert.AreEqual("org5", aff.Name, "Incorrect response for id");
            Assert.AreEqual(200, resp.StatusCode, "Incorrect status code");
        }

        // Tests updating an affiliation that doesn't require force option
        [TestMethod]
        public void TestUpdateAffiliationNoForce()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAAffiliation aff = client.NewHFCAAffiliation("org_5");
            aff.Create(admin);
            aff.UpdateName = "org_6";
            HFCAAffiliation.HFCAAffiliationResp resp = aff.Update(admin);

            Assert.AreEqual(200, resp.StatusCode, "Incorrect status code");
            Assert.AreEqual("org_6", aff.Name, "Failed to delete affiliation");
        }

        // Trying to update affiliations with child affiliations and identities
        // should fail if not using 'force' option.
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "Need to use 'force' to remove identities and affiliation")]
        public void TestUpdateAffiliationInvalid()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                throw new System.Exception("Need to use 'force' to remove identities and affiliation");
                // needs v1.1
            }

            HFCAAffiliation aff = client.NewHFCAAffiliation("org1.dept1");
            aff.Create(admin);

            HFCAAffiliation aff2 = aff.CreateDecendent("team1");
            aff2.Create(admin);

            HFCAIdentity ident = GetIdentityReq("testorg1dept1", "client");
            ident.Affiliation = aff.Name;
            ident.Create(admin);

            aff.UpdateName = "org1.dept2";
            HFCAAffiliation.HFCAAffiliationResp resp = aff.Update(admin);
            Assert.AreEqual(400, resp.StatusCode, "Incorrect status code");
        }

        // Tests deleting an affiliation
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "Affiliation has been deleted")]
        public void TestDeleteAffiliation()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                throw new System.Exception("Affiliation has been deleted");
                // needs v1.1
            }


            HFCAAffiliation aff = client.NewHFCAAffiliation("org6");
            aff.Create(admin);

            HFCAIdentity ident = client.NewHFCAIdentity("testuser_org6");
            ident.Affiliation = "org6";
            ident.Create(admin);

            HFCAAffiliation aff2 = client.NewHFCAAffiliation("org6.dept1");
            aff2.Create(admin);

            HFCAIdentity ident2 = client.NewHFCAIdentity("testuser_org6.dept1");
            ident2.Affiliation = "org6.dept1";
            ident2.Create(admin);

            HFCAAffiliation.HFCAAffiliationResp resp = aff.Delete(admin, true);
            int idCount = 0;
            bool found = false;
            foreach (HFCAAffiliation childAff in resp.Children)
            {
                if (childAff.Name.Equals("org6.dept1"))
                {
                    found = true;
                }

                foreach (HFCAIdentity id in childAff.Identities)
                {
                    if (id.EnrollmentId.Equals("testuser_org6.dept1"))
                    {
                        idCount++;
                    }
                }
            }

            foreach (HFCAIdentity id in resp.Identities)
            {
                if (id.EnrollmentId.Equals("testuser_org6"))
                {
                    idCount++;
                }
            }

            if (!found)
            {
                Assert.Fail("Incorrect response received");
            }

            if (idCount != 2)
            {
                Assert.Fail("Incorrect number of ids returned");
            }

            Assert.AreEqual(200, resp.StatusCode, "Incorrect status code");
            Assert.AreEqual("org6", aff.Name, "Failed to delete affiliation");

            aff.Delete(admin);
        }

        // Tests deleting an affiliation that doesn't require force option
        [TestMethod]
        public void TestDeleteAffiliationNoForce()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                return; // needs v1.1
            }

            HFCAAffiliation aff = client.NewHFCAAffiliation("org6");
            aff.Create(admin);
            HFCAAffiliation.HFCAAffiliationResp resp = aff.Delete(admin);

            Assert.AreEqual(200, resp.StatusCode, "Incorrect status code");
            Assert.AreEqual("org6", aff.Name, "Failed to delete affiliation");
        }

        // Trying to delete affiliation with child affiliations and identities should result
        // in an error without force option.
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "Authorization failure")]
        public void TestForceDeleteAffiliationInvalid()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                throw new System.Exception("Authorization failure");
                // needs v1.1
            }


            HFCAAffiliation aff = client.NewHFCAAffiliation("org1.dept3");
            aff.Create(admin);

            HFCAAffiliation aff2 = client.NewHFCAAffiliation("org1.dept3.team1");
            aff2.Create(admin);

            HFCAIdentity ident = GetIdentityReq("testorg1dept3", "client");
            ident.Affiliation = "org1.dept3";
            ident.Create(admin);

            HFCAAffiliation.HFCAAffiliationResp resp = aff.Delete(admin);
            Assert.AreEqual(401, resp.StatusCode, "Incorrect status code");
        }

        // Tests deleting an affiliation on CA that does not allow affiliation removal
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "Authorization failure")]
        public void TestDeleteAffiliationNotAllowed()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                throw new System.Exception("Authorization failure");
                // needs v1.1
            }

            HFCAClient client2 = HFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG2).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG2).CAProperties);
            client2.CryptoSuite = crypto;

            // SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface
            SampleUser admin2 = sampleStore.GetMember(TEST_ADMIN_NAME, "org2");
            if (!admin2.IsEnrolled)
            {
                // Preregistered admin only needs to be enrolled with Fabric CA.
                admin2.Enrollment = client2.Enroll(admin2.Name, TEST_ADMIN_PW);
            }

            HFCAAffiliation aff = client2.NewHFCAAffiliation("org6");
            HFCAAffiliation.HFCAAffiliationResp resp = aff.Delete(admin2);
            Assert.AreEqual(400, resp.StatusCode, "Incorrect status code");
        }

        // Tests getting server/ca information
        [TestMethod]
        public void TestGetInfo()
        {
            if (testConfig.IsRunningAgainstFabric10())
            {
                HFCAInfo info = client.Info();
                Assert.IsNull(info.Version);
            }

            if (!testConfig.IsRunningAgainstFabric10())
            {
                HFCAInfo info = client.Info();
                Assert.IsNotNull(info, "client.info returned null.");
                string version = info.Version;
                Assert.IsNotNull(version, "client.info.getVersion returned null.");
                Assert.IsTrue(Regex.Match(version, "^\\d+\\.\\d+\\.\\d+($|-.*)").Success, $"Version '{version}' didn't match expected pattern");
            }
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "Failed to enroll user")]
        public void TestEnrollNoKeyPair()
        {
            SampleUser user = GetEnrolledUser(TEST_ADMIN_ORG);

            EnrollmentRequest req = new EnrollmentRequest(HFCAClient.DEFAULT_PROFILE_NAME, "label 1", null);
            req.CSR = "test";
            client.Enroll(user.Name, user.EnrollmentSecret, req);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(RevocationException), "Error while revoking the user")]
        public void TestRevokeNotAuthorized()
        {
            // See if a normal user can revoke the admin...
            SampleUser user = GetEnrolledUser(TEST_ADMIN_ORG);
            client.Revoke(user, admin.Name, "revoke admin");
        }

        [TestMethod]
        public void TestEnrollSameUser()
        {
            // [ExpectedExceptionWithMessage(typeof(RevocationException),"does not have attribute 'hf.Revoker'")]

            // See if a normal user can revoke the admin...
            SampleUser user1 = GetEnrolledUser(TEST_ADMIN_ORG);

            string sampleStoreFile = Path.Combine(Path.GetTempPath(), "HFCSampletest.properties");
            if (File.Exists(sampleStoreFile))
            {
                // For testing start fresh
                File.Delete(sampleStoreFile);
            }

            sampleStore = new SampleStore(sampleStoreFile);

            SampleUser user2 = GetEnrolledUser(TEST_ADMIN_ORG);

            // client.revoke(user, admin.Name, "revoke admin");
            client.Enroll(user1.Name, user2.EnrollmentSecret);
        }

        // Tests enrolling a user to an unknown CA client
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "Failed to enroll user")]
        public void TestEnrollUnknownClient()
        {
            ICryptoSuite cryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();

            // This client does not exist
            string clientName = "test CA client";

            HFCAClient clientWithName = HFCAClient.Create(clientName, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CAProperties);
            clientWithName.CryptoSuite = cryptoSuite;

            clientWithName.Enroll(admin.Name, TEST_ADMIN_PW);
        }

        // revoke2: revoke(User revoker, String revokee, String reason)
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(RevocationException), "Error while revoking")]
        public void TestRevoke2UnknownUser()
        {
            client.Revoke(admin, "unknownUser", "remove user2");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "failed enrollment for user")]
        public void TestMockEnrollSuccessFalse()
        {
            MockHFCAClient mockClient = MockHFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CAProperties);
            mockClient.CryptoSuite = crypto;

            SampleUser user = GetEnrolledUser(TEST_ADMIN_ORG);

            mockClient.SetHttpPostResponse("{\"success\":false}");
            mockClient.Enroll(user.Name, user.EnrollmentSecret);
        }

        [Ignore]
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "failed enrollment for user")]
        public void TestMockEnrollNoCert()
        {
            MockHFCAClient mockClient = MockHFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CAProperties);
            mockClient.CryptoSuite = crypto;

            SampleUser user = GetEnrolledUser(TEST_ADMIN_ORG);

            mockClient.SetHttpPostResponse("{\"success\":true}");
            mockClient.Enroll(user.Name, user.EnrollmentSecret);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "response did not contain a result")]
        public void TestMockEnrollNoResult()
        {
            MockHFCAClient mockClient = MockHFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CAProperties);
            mockClient.CryptoSuite = crypto;

            SampleUser user = GetEnrolledUser(TEST_ADMIN_ORG);

            mockClient.SetHttpPostResponse("{\"success\":true}");
            mockClient.Enroll(user.Name, user.EnrollmentSecret);
        }

        [TestMethod]
        public void TestMockEnrollWithMessages()
        {
            MockHFCAClient mockClient = MockHFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CAProperties);
            mockClient.CryptoSuite = crypto;

            SampleUser user = GetEnrolledUser(TEST_ADMIN_ORG);

            mockClient.SetHttpPostResponse("{\"success\":true, \"result\":{\"Cert\":\"abc\"}, \"messages\":[{\"code\":123, \"message\":\"test message\"}]}");
            mockClient.Enroll(user.Name, user.EnrollmentSecret);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "failed")]
        public void TestMockReenrollNoResult()
        {
            MockHFCAClient mockClient = MockHFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CAProperties);
            mockClient.CryptoSuite = crypto;

            SampleUser user = GetEnrolledUser(TEST_ADMIN_ORG);

            mockClient.SetHttpPostResponse("{\"success\":true}");
            mockClient.Reenroll(user);
        }

        [Ignore]
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "failed re-enrollment for user")]
        public void TestMockReenrollNoCert()
        {
            MockHFCAClient mockClient = MockHFCAClient.Create(testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CALocation, testConfig.GetIntegrationTestsSampleOrg(TEST_WITH_INTEGRATION_ORG).CAProperties);
            mockClient.CryptoSuite = crypto;

            SampleUser user = GetEnrolledUser(TEST_ADMIN_ORG);

            mockClient.SetHttpPostResponse("{\"success\":true}");
            mockClient.Reenroll(user);
        }

        // ==========================================================================================
        // Helper methods
        // ==========================================================================================

        private void VerifyOptions(string cert, EnrollmentRequest req)
        {
            try
            {
                X509Certificate2 certificate = crypto.BytesToCertificate(cert.ToBytes());


                // check Subject Alternative Names
                List<string> altNames = ParseSujectAlternativeNames(certificate).ToList();
                if (altNames.Count == 0)
                {
                    if (req.Hosts != null && req.Hosts.Count > 0)
                    {
                        Assert.Fail("Host name is not included in certificate");
                    }

                    return;
                }

                List<string> subAltList = req.Hosts.ToList();
                foreach (string s in altNames)
                {
                    if (req.Hosts.Contains(s))
                    {
                        subAltList.Remove(s);
                    }
                }

                if (subAltList.Count > 0)
                {
                    Assert.Fail("Subject Alternative Names not matched the host names specified in enrollment request");
                }
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Cannot parse certificate. Error is: {e.Message}");
            }
        }

        public static IEnumerable<string> ParseSujectAlternativeNames(X509Certificate2 cert)
        {
            Regex sanRex = new Regex(@"^DNS Name=(.*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

            var sanList = cert.Extensions.Cast<X509Extension>().Where(ext => ext.Oid.FriendlyName.Equals("Subject Alternative Name", StringComparison.Ordinal)).Select(ext => new {ext, data = new AsnEncodedData(ext.Oid, ext.RawData)}).Select(@t => new {@t, text = @t.data.Format(true)}).SelectMany(@t => @t.text.Split(new char[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries), (@t, line) => new {@t, line}).Select(@t => new {@t, match = sanRex.Match(@t.line)}).Where(@t => @t.match.Success && @t.match.Groups.Count > 0 && !string.IsNullOrEmpty(@t.match.Groups[1].Value)).Select(@t => @t.match.Groups[1].Value);

            return sanList;
        }

        // Returns a new (unique) user for use in a single test
        private SampleUser GetTestUser(string org)
        {
            string userName = userNamePrefix + ++userCount;
            return sampleStore.GetMember(userName, org);
        }

        // Returns an enrolled user
        private SampleUser GetEnrolledUser(string org)
        {
            SampleUser user = GetTestUser(org);
            RegistrationRequest rr = new RegistrationRequest(user.Name, TEST_USER1_AFFILIATION);
            string password = "password";
            rr.Secret = password;
            user.EnrollmentSecret = client.Register(rr, admin);
            if (!user.EnrollmentSecret.Equals(password))
            {
                Assert.Fail($"Secret returned from RegistrationRequest not match : {user.EnrollmentSecret}");
            }

            user.Enrollment = client.Enroll(user.Name, user.EnrollmentSecret);
            return user;
        }

        private HFCAIdentity GetIdentityReq(string enrollmentID, string type)
        {
            string password = "password";

            HFCAIdentity ident = client.NewHFCAIdentity(enrollmentID);
            ident.Secret = password;
            ident.Affiliation = TEST_USER1_AFFILIATION;
            ident.MaxEnrollments = 1;
            ident.Type = type;

            List<Attribute> attributes = new List<Attribute>();
            attributes.Add(new Attribute("testattr1", "valueattr1"));
            ident.Attributes = attributes;
            return ident;
        }

        private void SleepALittle()
        {
            // Seems to be an odd that calling back too quickly can once in a while generate an error on the fabric_ca
            // try {
            // Thread.sleep(5000);
            // } catch (InterruptedException e) {
            // e.printStackTrace();
            // }
        }
    }
}