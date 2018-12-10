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

using System;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric_CA.SDK.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK_CA
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class EnrollmentRequestTest
    {
        private static readonly string caName = "certsInc";
        private static readonly string csr = "11436845810603";
        private static readonly string profile = "test profile";
        private static readonly string label = "test label";
        private static readonly KeyPair keyPair = null;

        [TestMethod]
        public void TestNewInstanceEmpty()
        {
            try
            {
                EnrollmentRequest testEnrollReq = new EnrollmentRequest();
                Assert.IsNull(testEnrollReq.CSR);
                Assert.IsTrue(testEnrollReq.Hosts.Count == 0);
                Assert.IsNull(testEnrollReq.Profile);
                Assert.IsNull(testEnrollReq.Label);
                Assert.IsNull(testEnrollReq.KeyPair);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestNewInstanceParms()
        {
            try
            {
                EnrollmentRequest testEnrollReq = new EnrollmentRequest(profile, label, keyPair);
                Assert.IsNull(testEnrollReq.CSR);
                Assert.IsTrue(testEnrollReq.Hosts.Count == 0);
                Assert.AreEqual(testEnrollReq.Profile, profile);
                Assert.AreEqual(testEnrollReq.Label, label);
                Assert.IsNull(testEnrollReq.KeyPair);
            }
            catch (System.Exception e)
            {
                Assert.Fail("Unexpected Exception " + e.Message);
            }
        }

        [TestMethod]
        public void TestEnrollReqSetGet()
        {
            try
            {
                EnrollmentRequest testEnrollReq = new EnrollmentRequest();
                testEnrollReq.AddHost("d.com");
                // set csr
                testEnrollReq.CSR = csr;
                testEnrollReq.Profile = profile;
                testEnrollReq.Label = label;
                testEnrollReq.KeyPair = null;
                testEnrollReq.CAName = caName;
                Assert.AreEqual(testEnrollReq.CSR, csr);
                Assert.IsTrue(testEnrollReq.Hosts.Contains("d.com"));
                Assert.AreEqual(testEnrollReq.Profile, profile);
                Assert.AreEqual(testEnrollReq.Label, label);
                Assert.IsNull(testEnrollReq.KeyPair);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestEnrollReqToJson()
        {
            try
            {
                EnrollmentRequest testEnrollReq = new EnrollmentRequest();
                testEnrollReq.AddHost("d.com");
                testEnrollReq.CSR = csr;
                testEnrollReq.Profile = profile;
                testEnrollReq.Label = label;
                testEnrollReq.KeyPair = null;
                testEnrollReq.CAName = caName;

                Assert.IsTrue(testEnrollReq.ToJson().Contains(csr));
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestEnrollReqToJsonAttr()
        {
            EnrollmentRequest testEnrollReq = new EnrollmentRequest();
            testEnrollReq.AddHost("d.com");
            testEnrollReq.CSR = csr;
            testEnrollReq.Profile = profile;
            testEnrollReq.Label = label;
            testEnrollReq.KeyPair = null;
            testEnrollReq.CAName = caName;
            testEnrollReq.AddAttrReq("foo");
            testEnrollReq.AddAttrReq("foorequired").SetOptional(false);
            testEnrollReq.AddAttrReq("foofalse").SetOptional(true);

            string s = testEnrollReq.ToJson();
            Assert.IsNotNull(s);
            Console.WriteLine(s);
            Assert.IsTrue(s.Contains("\"attr_reqs\":["));
            Assert.IsTrue(s.Contains("\"name\":\"foorequired\",\"optional\":false"));
            Assert.IsTrue(s.Contains("\"name\":\"foofalse\",\"optional\":true"));
        }

        [TestMethod]
        public void TestEnrollReqToJsonAttrNotThere()
        {
            EnrollmentRequest testEnrollReq = new EnrollmentRequest();
            testEnrollReq.AddHost("d.com");
            testEnrollReq.CSR = csr;
            testEnrollReq.Profile = profile;
            testEnrollReq.Label = label;
            testEnrollReq.KeyPair = null;
            testEnrollReq.CAName = caName;

            string s = testEnrollReq.ToJson();
            Assert.IsNotNull(s);
            Assert.IsFalse(s.Contains("\"attr_reqs\":["));
        }

        [TestMethod]
        public void TestEnrollReqToJsonAttrEmpty()
        {
            EnrollmentRequest testEnrollReq = new EnrollmentRequest();
            testEnrollReq.AddHost("d.com");
            testEnrollReq.CSR = csr;
            testEnrollReq.Profile = profile;
            testEnrollReq.Label = label;
            testEnrollReq.KeyPair = null;
            testEnrollReq.CAName = caName;
            testEnrollReq.AddAttrReq(); // means empty. force no attributes.

            string s = testEnrollReq.ToJson();
            Assert.IsNotNull(s);
            Assert.IsTrue(s.Contains("\"attr_reqs\":[]") || !s.Contains("\"attr_reqs\""));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "name may not be null or empty.")]
        public void TestEnrollReqToJsonAttrNullName()
        {
            EnrollmentRequest testEnrollReq = new EnrollmentRequest();
            testEnrollReq.AddAttrReq(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "name may not be null or empty.")]
        public void TestEnrollReqToJsonAttrEmptyName()
        {
            EnrollmentRequest testEnrollReq = new EnrollmentRequest();
            testEnrollReq.AddAttrReq("");
        }
    }
}