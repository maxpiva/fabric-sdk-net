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


using System;
using System.IO;
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
    public class HFCAIdentityTest
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
                throw new System.Exception("HFCAIdentityTest.setupBeforeClass failed!", e);
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
            HFCAIdentity ident = client.NewHFCAIdentity("testid");

            Assert.IsNotNull(ident);
            Assert.AreSame(typeof(HFCAIdentity), ident.GetType());
            Assert.AreEqual(ident.EnrollmentId, "testid");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "Client's crypto primitives not set")]
        public void TestHFCAIdentityCryptoNull()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = null;
            client.NewHFCAIdentity("testid");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "EnrollmentID cannot be null or empty")]
        public void TestHFCAIdentityIDNull()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.NewHFCAIdentity(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IdentityException), "Error while getting user")]
        public void GetIdentityNoServerResponse()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;

            HFCAIdentity ident = client.NewHFCAIdentity("testuser1");
            ident.Read(admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IdentityException), "Error while creating user")]
        public void CreateIdentityNoServerResponse()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;

            HFCAIdentity ident = client.NewHFCAIdentity("testuser1");
            ident.Create(admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IdentityException), "Error while updating user")]
        public void UpdateIdentityNoServerResponse()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;

            HFCAIdentity ident = client.NewHFCAIdentity("testuser1");
            ident.Update(admin);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IdentityException), "Error while deleting user")]
        public void DeleteIdentityNoServerResponse()
        {
            HFCAClient client = HFCAClient.Create("http://localhost:99", null);
            client.CryptoSuite = crypto;

            HFCAIdentity ident = client.NewHFCAIdentity("testuser1");
            ident.Delete(admin);
        }
    }
}