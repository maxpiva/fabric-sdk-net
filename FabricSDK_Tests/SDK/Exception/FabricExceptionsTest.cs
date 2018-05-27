/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *  http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

using Google.Protobuf;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Exception
{
    [TestClass]
    [TestCategory("SDK")]
    public class FabricExceptionsTest
    {
        private static readonly string MESSAGE = "test";


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ChaincodeEndorsementPolicyParseException), "test")]
        public void TestChaincodeEndorsementPolicyParseException1()
        {
            throw new ChaincodeEndorsementPolicyParseException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ChaincodeEndorsementPolicyParseException), "test")]
        public void testChaincodeEndorsementPolicyParseException2()
        {
            throw new ChaincodeEndorsementPolicyParseException(MESSAGE, new ChaincodeEndorsementPolicyParseException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ChaincodeException), "test")]
        public void TestChaincodeException()
        {
            System.Exception baseException = new System.Exception(MESSAGE);
            throw new ChaincodeException(MESSAGE, baseException);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(CryptoException), "test")]
        public void TestCryptoException1()
        {
            throw new CryptoException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(CryptoException), "test")]
        public void TestCryptoException2()
        {
            throw new CryptoException(MESSAGE, new CryptoException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EventHubException), "test")]
        public void TestEventHubException1()
        {
            throw new EventHubException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EventHubException), "test")]
        public void TestEventHubException2()
        {
            throw new EventHubException(new CryptoException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EventHubException), "test")]
        public void TestEventHubException3()
        {
            throw new EventHubException(MESSAGE, new CryptoException(MESSAGE));
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ExecuteException), "test")]
        public void TestExecuteException1()
        {
            throw new ExecuteException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ExecuteException), "test")]
        public void TestExecuteException2()
        {
            throw new ExecuteException(MESSAGE, new ExecuteException(MESSAGE));
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(GetTCertBatchException), "test")]
        public void TestGetTCertBatchException()
        {
            throw new GetTCertBatchException(MESSAGE, new ExecuteException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "test")]
        public void TestInvalidArgumentException1()
        {
            throw new InvalidArgumentException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "test")]
        public void TestInvalidArgumentException2()
        {
            throw new InvalidArgumentException(new InvalidArgumentException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidArgumentException), "test")]
        public void TestInvalidArgumentException3()
        {
            throw new InvalidArgumentException(MESSAGE, new InvalidArgumentException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "test")]
        public void TestIllegalArgumentException1()
        {
            throw new IllegalArgumentException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "test")]
        public void TestIllegalArgumentException2()
        {
            throw new IllegalArgumentException(new IllegalArgumentException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "test")]
        public void TestIllegalArgumentException3()
        {
            throw new IllegalArgumentException(MESSAGE, new IllegalArgumentException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidTransactionException), "test")]
        public void TestInvalidTransactionException1()
        {
            throw new InvalidTransactionException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidTransactionException), "test")]
        public void TestInvalidTransactionException2()
        {
            throw new InvalidTransactionException(MESSAGE, new InvalidTransactionException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvokeException), "test")]
        public void testInvokeException()
        {
            System.Exception baseException = new System.Exception(MESSAGE);


            throw new InvokeException(MESSAGE, baseException);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(NoAvailableTCertException), "test")]
        public void TestNoAvailableTCertException()
        {
            throw new NoAvailableTCertException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(NoValidPeerException), "test")]
        public void testNoValidPeerException()
        {
            throw new NoValidPeerException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(PeerException), "test")]
        public void testPeerException1()
        {
            throw new PeerException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(PeerException), "test")]
        public void testPeerException2()
        {
            throw new PeerException(MESSAGE, new PeerException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "test")]
        public void testProposalException1()
        {
            throw new ProposalException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "test")]
        public void testProposalException2()
        {
            throw new ProposalException(new ProposalException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ProposalException), "test")]
        public void testProposalException3()
        {
            throw new ProposalException(MESSAGE, new ProposalException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(QueryException), "test")]
        public void testQueryException()
        {
            System.Exception baseException = new System.Exception(MESSAGE);


            throw new QueryException(MESSAGE, baseException);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(TransactionException), "test")]
        public void testTransactionException1()
        {
            throw new TransactionException(MESSAGE);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(TransactionException), "test")]
        public void testTransactionException2()
        {
            throw new TransactionException(new TransactionException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(TransactionException), "test")]
        public void testTransactionException3()
        {
            throw new TransactionException(MESSAGE, new TransactionException(MESSAGE));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(TransactionException), "test")]
        public void testTransactionEventException1()
        {
            throw new TransactionEventException(MESSAGE, null);
        }


        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(TransactionEventException), "test")]
        public void testTransactionEventException2()
        {
            TransactionEventException e = new TransactionEventException(MESSAGE, null);
            Assert.IsNull(e.TransactionEvent);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(TransactionEventException), "test")]
        public void testTransactionEventException3()
        {
            throw new TransactionEventException(MESSAGE, null, new TransactionEventException(MESSAGE, null));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidProtocolBufferRuntimeException), "test")]
        public void testInvalidProtocolBufferRuntimeException1()
        {
            throw new InvalidProtocolBufferRuntimeException(default(InvalidProtocolBufferException));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidProtocolBufferRuntimeException), "test")]
        public void testInvalidProtocolBufferRuntimeException2()
        {
            throw new InvalidProtocolBufferRuntimeException(MESSAGE, default(InvalidProtocolBufferException));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(InvalidProtocolBufferRuntimeException), "test")]
        public void testInvalidProtocolBufferRuntimeException3()
        {
            InvalidProtocolBufferException e1 = default(InvalidProtocolBufferException);
            InvalidProtocolBufferRuntimeException e2 = new InvalidProtocolBufferRuntimeException(MESSAGE, e1);

            Assert.AreEqual(e1, e2.InnerException);
        }
    }
}