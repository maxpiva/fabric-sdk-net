/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
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

using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK_CA
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class RevocationRequestTest
    {
        private static readonly string revCAName = "CA";
        private static readonly string revEnrollmentID = "userid";
        private static readonly string revSerialNmbr = "987654321";
        private static readonly string revAKI = "123456789";
        private static readonly string revReason = "compromised";
        private static readonly bool revGenCRL = true;

        [TestMethod]
        public void TestNewInstance()
        {
            try
            {
                RevocationRequest testRevocationReq = new RevocationRequest(revCAName, revEnrollmentID, revSerialNmbr, revAKI, revReason, revGenCRL);
                Assert.AreEqual(testRevocationReq.User, revEnrollmentID);
                Assert.AreEqual(testRevocationReq.Serial, revSerialNmbr);
                Assert.AreEqual(testRevocationReq.Aki, revAKI);
                Assert.AreEqual(testRevocationReq.Reason, revReason);
                Assert.AreEqual(testRevocationReq.GenCRL, revGenCRL);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "Enrollment ID is empty, thus both aki and serial must have non-empty values")]
        public void TestNewInstanceSetNullIDSerialNmbr()
        {
            new RevocationRequest(revCAName, null, null, revAKI, revReason);
            Assert.Fail("Expected exception when null is specified for serial number");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "Enrollment ID is empty, thus both aki and serial must have non-empty values")]
        public void TestNewInstanceSetNullIDAKI()
        {
            new RevocationRequest(revCAName, null, revSerialNmbr, null, revReason);
            Assert.Fail("Expected exception when null is specified for AKI");
        }

        [TestMethod]
        public void TestRevocationReqSetGet()
        {
            try
            {
                RevocationRequest testRevocationReq = new RevocationRequest(revCAName, revEnrollmentID, revSerialNmbr, revAKI, revReason);
                testRevocationReq.User = revEnrollmentID + "update";
                testRevocationReq.Serial = revSerialNmbr + "000";
                testRevocationReq.Aki = revAKI + "000";
                testRevocationReq.Reason = revReason + "update";
                Assert.AreEqual(testRevocationReq.User, revEnrollmentID + "update");
                Assert.AreEqual(testRevocationReq.Serial, revSerialNmbr + "000");
                Assert.AreEqual(testRevocationReq.Aki, revAKI + "000");
                Assert.AreEqual(testRevocationReq.Reason, revReason + "update");
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestRevocationReqToJsonNullID()
        {
            try
            {
                RevocationRequest testRevocationReq = new RevocationRequest(revCAName, null, revSerialNmbr, revAKI, revReason);
                testRevocationReq.Serial = revSerialNmbr;
                testRevocationReq.Aki = revAKI + "000";
                testRevocationReq.Reason = revReason + "update";

                Assert.IsTrue(testRevocationReq.ToJson().Contains(revSerialNmbr));
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestRevocationReqToJson()
        {
            try
            {
                RevocationRequest testRevocationReq = new RevocationRequest(revCAName, revEnrollmentID, revSerialNmbr, revAKI, revReason);
                testRevocationReq.User = revEnrollmentID + "update";
                testRevocationReq.Serial = revSerialNmbr + "000";
                testRevocationReq.Aki = revAKI + "000";
                testRevocationReq.Reason = revReason + "update";

                Assert.IsTrue(testRevocationReq.ToJson().Contains(revReason + "update"));
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }
    }
}