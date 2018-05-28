/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class ClientTest
    {
        private static readonly string CHANNEL_NAME = "channel1";
        private static HFClient hfclient = null;

        private static readonly string USER_NAME = "MockMe";
        private static readonly string USER_MSP_ID = "MockMSPID";


        [ClassInitialize]
        public static void SetupClient(TestContext context)
        {
            try
            {
                TestUtils.TestUtils.ResetConfig();
                hfclient = TestHFClient.Create();
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestNewChannel()
        {
            try
            {
                Channel testChannel = hfclient.NewChannel(CHANNEL_NAME);
                Assert.IsTrue(testChannel != null && CHANNEL_NAME.Equals(testChannel.Name, StringComparison.InvariantCultureIgnoreCase));
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestSetNullChannel()
        {
            hfclient.NewChannel(null);
            Assert.Fail("Expected null channel to throw exception.");
        }

        [TestMethod]
        public void TestNewPeer()
        {
            try
            {
                Peer peer = hfclient.NewPeer("peer_", "grpc://localhost:7051");
                Assert.IsTrue(peer != null);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadURL()
        {
            hfclient.NewPeer("peer_", " ");
            Assert.Fail("Expected peer with no channel throw exception");
        }

        [TestMethod]
        public void TestNewOrderer()
        {
            try
            {
                Orderer orderer = hfclient.NewOrderer("xx", "grpc://localhost:5005");
                Assert.IsTrue(orderer != null);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadAddress()
        {
            hfclient.NewOrderer("xx", "xxxxxx");
            Assert.Fail("Orderer allowed setting bad URL.");
        }


        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadCryptoSuite()
        {
            HFClient.Create().NewOrderer("xx", "xxxxxx");
            Assert.Fail("Orderer allowed setting no cryptoSuite");
        }

        [TestMethod]
        public void TestGoodMockUser()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
            client.UserContext = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);
            Orderer orderer = hfclient.NewOrderer("justMockme", "grpc://localhost:99"); // test mock should work.
            Assert.IsNotNull(orderer);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadUserContextNull()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();

            client.UserContext = null;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadUserNameNull()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();

            TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser(null, USER_MSP_ID);

            client.UserContext = mockUser;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadUserNameEmpty()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
            ;

            TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser("", USER_MSP_ID);

            client.UserContext = mockUser;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadUserMSPIDNull()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
            ;

            TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser(USER_NAME, null);

            client.UserContext = mockUser;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadUserMSPIDEmpty()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
            ;

            TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser(USER_NAME, "");


            client.UserContext = mockUser;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadEnrollmentNull()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
            ;

            TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);
            mockUser.Enrollment = null;
            client.UserContext = mockUser;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadEnrollmentBadCert()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
            ;

            TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);

            TestUtils.TestUtils.MockEnrollment mockEnrollment = TestUtils.TestUtils.GetMockEnrollment(null);
            mockUser.Enrollment = mockEnrollment;
            client.UserContext = mockUser;
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestBadEnrollmentBadKey()
        {
            HFClient client = HFClient.Create();
            client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
            ;


            TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);

            TestUtils.TestUtils.MockEnrollment mockEnrollment = TestUtils.TestUtils.GetMockEnrollment(null, "mockCert");
            mockUser.Enrollment = mockEnrollment;
            client.UserContext = mockUser;
        }

        [Ignore]
        [TestMethod]
        [ExpectedException(typeof(InvalidArgumentException))]
        public void TestCryptoFactory()
        {
            try
            {
                TestUtils.TestUtils.ResetConfig();
                Assert.IsNotNull(Config.Instance.GetDefaultCryptoSuiteFactory());

                HFClient client = HFClient.Create();

                client.CryptoSuite = HLSDKJCryptoSuiteFactory.Instance.GetCryptoSuite();
                ;

                TestUtils.TestUtils.MockUser mockUser = TestUtils.TestUtils.GetMockUser(USER_NAME, USER_MSP_ID);

                TestUtils.TestUtils.MockEnrollment mockEnrollment = TestUtils.TestUtils.GetMockEnrollment(null, "mockCert");
                mockUser.Enrollment = mockEnrollment;
                client.UserContext = mockUser;
            }
            finally
            {
                // System.getProperties().remove("org.hyperledger.fabric.sdk.crypto.default_crypto_suite_factory");
            }
        }
    }
}