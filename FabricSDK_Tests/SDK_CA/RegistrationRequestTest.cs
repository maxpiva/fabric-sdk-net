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
using Hyperledger.Fabric_CA.SDK;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric.Tests.SDK_CA
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class RegistrationRequestTest
    {
        private static readonly string attrName = "some name";
        private static readonly string attrValue = "some value";
        private static readonly string regAffiliation = "corporation";
        private static readonly string regCAName = "CA";
        private static readonly string regID = "userid";
        private static readonly string regSecret = "secret";
        private static readonly string regType = HFCAClient.HFCA_TYPE_CLIENT;

        private static readonly int regMaxEnrollments = 5;

        [TestMethod]
        public void TestNewInstance()
        {
            try
            {
                RegistrationRequest testRegisterReq = new RegistrationRequest(regID, regAffiliation);
                Assert.AreEqual(testRegisterReq.EnrollmentID, regID);
                Assert.AreEqual(testRegisterReq.Type, regType);
                Assert.AreEqual(testRegisterReq.MaxEnrollments, null);
                Assert.AreEqual(testRegisterReq.Affiliation, regAffiliation);
                Assert.IsTrue(testRegisterReq.Attributes.Count == 0);

                JObject jo = testRegisterReq.ToJsonObject();

                Assert.AreEqual(jo["affiliation"].Value<string>(), regAffiliation);
                Assert.IsFalse(jo.ContainsKey("max_enrollments"));
                Assert.AreEqual(regID, jo["id"].Value<string>());
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestNewInstanceNoAffiliation()
        {
            try
            {
                RegistrationRequest testRegisterReq = new RegistrationRequest(regID);
                testRegisterReq.MaxEnrollments = 3;

                Assert.AreEqual(regID, testRegisterReq.EnrollmentID);
                Assert.AreEqual(regType, testRegisterReq.Type);
                Assert.AreEqual(3, testRegisterReq.MaxEnrollments);
                Assert.AreEqual(null, testRegisterReq.Affiliation);
                Assert.IsTrue(testRegisterReq.Attributes.Count == 0);

                JObject jo = testRegisterReq.ToJsonObject();
                Assert.IsFalse(jo.ContainsKey("affiliation"));
                Assert.AreEqual(3, jo["max_enrollments"].Value<int>());
                Assert.AreEqual(regID, jo["id"].Value<string>());
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "id may not be null")]
        public void TestNewInstanceSetNullID()
        {
            new RegistrationRequest(null, regAffiliation);
            Assert.Fail("Expected exception when null is specified for id");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(System.Exception), "affiliation may not be null")]
        public void TestNewInstanceSetNullAffiliation()
        {
            new RegistrationRequest(regID, null);
            Assert.Fail("Expected exception when null is specified for affiliation");
        }

        [TestMethod]
        public void TestRegisterReqSetGet()
        {
            try
            {
                RegistrationRequest testRegisterReq = new RegistrationRequest(regID, regAffiliation);
                testRegisterReq.EnrollmentID = regID + "update";
                testRegisterReq.Secret = regSecret;
                testRegisterReq.MaxEnrollments = regMaxEnrollments;
                testRegisterReq.Type = regType;
                testRegisterReq.Affiliation = regAffiliation + "update";
                testRegisterReq.CAName = regCAName;
                testRegisterReq.AddAttribute(new Attribute(attrName, attrValue));
                Assert.AreEqual(testRegisterReq.EnrollmentID, regID + "update");
                Assert.AreEqual(testRegisterReq.Secret, regSecret);
                Assert.AreEqual(testRegisterReq.Type, regType);
                Assert.AreEqual(testRegisterReq.Affiliation, regAffiliation + "update");
                Assert.IsTrue(testRegisterReq.Attributes.Count > 0);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestRegisterReqToJson()
        {
            try
            {
                RegistrationRequest testRegisterReq = new RegistrationRequest(regID, regAffiliation);
                testRegisterReq.EnrollmentID = regID + "update";
                testRegisterReq.Secret = regSecret;
                testRegisterReq.MaxEnrollments = regMaxEnrollments;
                testRegisterReq.Type = regType;
                testRegisterReq.Affiliation = regAffiliation + "update";
                testRegisterReq.CAName = regCAName;
                testRegisterReq.AddAttribute(new Attribute(attrName, attrValue));

                Assert.IsTrue(testRegisterReq.ToJson().Contains(regAffiliation + "update"));
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }
    }
}