/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *        http://www.apache.org/licenses/LICENSE-2.0
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
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class ChaincodeEndorsementPolicyTest
    {
        /**
     * Test method for {@link org.hyperledger.fabric.sdk.ChaincodeEndorsementPolicy#ChaincodeEndorsementPolicy()}.
     */
        [TestMethod]
        public void TestPolicyCtor()
        {
            ChaincodeEndorsementPolicy nullPolicy = new ChaincodeEndorsementPolicy();
            Assert.IsNull(nullPolicy.ChaincodeEndorsementPolicyAsBytes);
        }

        /**
     * Test method for {@link org.hyperledger.fabric.sdk.ChaincodeEndorsementPolicy#fromFile(File)} (java.io.File)}.
     *
     * @throws IOException
     */
        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestPolicyCtorFile()
        {
            ChaincodeEndorsementPolicy policy = new ChaincodeEndorsementPolicy();
            policy.FromFile(new FileInfo("/does/not/exists"));
        }

        /**
     * Test method for {@link org.hyperledger.fabric.sdk.ChaincodeEndorsementPolicy#fromFile(File)} (java.io.File)}.
     *
     * @throws IOException
     */
        [TestMethod]
        public void TestPolicyCtorValidFile()
        {
            FileInfo policyFile = new FileInfo("Resources/policyBitsAdmin");
            ChaincodeEndorsementPolicy policy = new ChaincodeEndorsementPolicy();
            policy.FromFile(policyFile);
            byte[] policyBits = File.ReadAllBytes(policyFile.FullName);
            CollectionAssert.AreEqual(policyBits, policy.ChaincodeEndorsementPolicyAsBytes);
        }

        /**
         * Test method for {@link org.hyperledger.fabric.sdk.ChaincodeEndorsementPolicy#fromBytes(byte[])}.
         */
        [TestMethod]
        public void TestPolicyCtorByteArray()
        {
            byte[] testInput = "this is a test".ToBytes();
            ChaincodeEndorsementPolicy fakePolicy = new ChaincodeEndorsementPolicy();
            fakePolicy.FromBytes(testInput);
            CollectionAssert.AreEqual(fakePolicy.ChaincodeEndorsementPolicyAsBytes, testInput);
        }

        /**
         * Test method for {@link ChaincodeEndorsementPolicy#fromYamlFile(File)}
         * @throws IOException
         * @throws ChaincodeEndorsementPolicyParseException
         */
        [TestMethod]
        public void TestSDKIntegrationYaml()
        {
            ChaincodeEndorsementPolicy itTestPolicy = new ChaincodeEndorsementPolicy();
            itTestPolicy.FromYamlFile(new FileInfo("Fixture/sdkintegration/chaincodeendorsementpolicy.yaml"));
            SignaturePolicyEnvelope sigPolEnv = SignaturePolicyEnvelope.Parser.ParseFrom(itTestPolicy.ChaincodeEndorsementPolicyAsBytes);
            List<MSPPrincipal> identitiesList = sigPolEnv.Identities.ToList();
            foreach (MSPPrincipal ident in identitiesList)
            {
                MSPPrincipal mspPrincipal = MSPPrincipal.Parser.ParseFrom(ident.Principal);
                MSPPrincipal.Types.Classification principalClassification = mspPrincipal.PrincipalClassification;
                Assert.AreEqual(principalClassification.ToString(), MSPPrincipal.Types.Classification.Role.ToString());
                MSPRole mspRole = MSPRole.Parser.ParseFrom(ident.Principal);
                string iden = mspRole.MspIdentifier;
                Assert.IsTrue("Org1MSP".Equals(iden) || "Org2MSP".Equals(iden));
                Assert.IsTrue(mspRole.Role == MSPRole.Types.MSPRoleType.Admin || mspRole.Role == MSPRole.Types.MSPRoleType.Member);
            }

            SignaturePolicy rule = sigPolEnv.Rule;
            SignaturePolicy.TypeOneofCase typeCase = rule.TypeCase;
            Assert.Equals(SignaturePolicy.TypeOneofCase.NOutOf, typeCase);
        }

        [TestMethod]
        public void TestBadYaml()
        {
            try
            {
                ChaincodeEndorsementPolicy itTestPolicy = new ChaincodeEndorsementPolicy();
                itTestPolicy.FromYamlFile(new FileInfo("fixture/sample_chaincode_endorsement_policies/badusertestCCEPPolicy.yaml"));

                Assert.Fail("Expected ChaincodeEndorsementPolicyParseException");
            }
            catch (ChaincodeEndorsementPolicyParseException e)
            {
            }
            catch (System.Exception e)
            {
                Assert.Fail("Expected ChaincodeEndorsementPolicyParseException");
            }
        }

        //src/test/fixture/sample_chaincode_endorsement_policies/badusertestCCEPPolicy.yaml

//    /**
//     * Test method for {@link org.hyperledger.fabric.sdk.ChaincodeEndorsementPolicy#fromBytes(byte[])}.
//     */
//    @Test
//    public void testSetPolicy() {
//        byte[] testInput = "this is a test".getBytes(UTF_8);
//        ChaincodeEndorsementPolicy fakePolicy = new ChaincodeEndorsementPolicy() ;
//        fakePolicy.fromBytes(testInput);
//        assertEquals(fakePolicy.getChaincodeEndorsementPolicyAsBytes(), testInput);
//    }
    }
}