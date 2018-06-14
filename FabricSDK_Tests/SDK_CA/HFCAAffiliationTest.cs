/*
 *  Copyright 2016, 2017, 2018 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *    http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using System.IO;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric.Tests.SDK.Integration;
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK_CA
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class HFCAAffiliationTest
    {
        private static readonly string TEST_ADMIN_NAME = "admin";
        private static readonly string TEST_ADMIN_ORG = "org1";

        private static CryptoPrimitives crypto;
        private SampleUser admin;

        private SampleStore sampleStore;

        [ClassInitialize]
        public static void SetupBeforeClass(TestContext context)
        {
            try
            {
                crypto = new CryptoPrimitives();
                crypto.Init();
            }
            catch (System.Exception e)
            {
                throw new System.Exception("HFCAAffiliationTest.setupBeforeClass failed!", e);
            }
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

            // SampleUser can be any implementation that implements org.hyperledger.fabric.sdk.User Interface
            admin = sampleStore.GetMember(TEST_ADMIN_NAME, TEST_ADMIN_ORG);
        }

        [TestMethod]
        public void TestHFCAIdentityNewInstance()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            HFCAAffiliation aff = client.NewHFCAAffiliation("org1");

            Assert.IsNotNull(aff);
            Assert.AreSame(typeof(HFCAAffiliation), aff.GetType());
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Crypto primitives not set")]
        public void TestHFCAIdentityCryptoNull()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = null;
            client.NewHFCAAffiliation("org1");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Affiliation name cannot be null or empty")]
        public void TestHFCAIdentityIDNull()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.NewHFCAAffiliation(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Affiliation name cannot contain an empty space or tab")]
        public void TestBadAffiliationNameSpace()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.NewHFCAAffiliation("foo. .bar");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Affiliation name cannot start with a dot '.'")]
        public void TestBadAffiliationNameStartingDot()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.NewHFCAAffiliation(".foo");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Affiliation name cannot end with a dot '.'")]
        public void TestBadAffiliationNameEndingDot()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.NewHFCAAffiliation("foo.");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Affiliation name cannot contain multiple consecutive dots '.'")]
        public void TestBadAffiliationNameMultipleDots()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            client.NewHFCAAffiliation("foo...bar");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(AffiliationException), "Error while getting affiliation")]
        public void GetAffiliationNoServerResponse()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            HFCAAffiliation aff = client.NewHFCAAffiliation("neworg1");
            aff.Read(admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(AffiliationException), "Error while creating affiliation")]
        public void CreateAffiliationNoServerResponse()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            HFCAAffiliation aff = client.NewHFCAAffiliation("neworg1");
            aff.Create(admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(AffiliationException), "Error while updating affiliation")]
        public void UpdateAffiliationNoServerResponse()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;

            HFCAAffiliation aff = client.NewHFCAAffiliation("neworg1");
            aff.UpdateName = "neworg1";
            aff.Update(admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(AffiliationException), "Error while deleting affiliation")]
        public void DeleteAffiliationNoServer()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;
            HFCAAffiliation aff = client.NewHFCAAffiliation("neworg1");
            aff.Delete(admin);
        }
    }
}