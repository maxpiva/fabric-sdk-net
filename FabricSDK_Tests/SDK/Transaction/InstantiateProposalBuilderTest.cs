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

using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Transaction
{
    [TestClass]
    [TestCategory("SDK")]
    public class InstantiateProposalBuilderTest
    {
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Transient map may not be null")]
        public void TestSetTransientMapNull()
        {
            InstantiateProposalBuilder builder = InstantiateProposalBuilder.Create();
            builder.SetTransientMap(null);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "IO Error")]
        public void TestBuild()
        {
            InstantiateProposalBuilder builder = InstantiateProposalBuilder.Create();
            builder.Build();
        }

        /*
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "Chaincode type is required")]

        public void TestInvalidType()
        {


            InstantiateProposalBuilder builder = InstantiateProposalBuilder.Create();
            builder.ChaincodeType(null);

        builder.Build();
    }

    */
    }
}