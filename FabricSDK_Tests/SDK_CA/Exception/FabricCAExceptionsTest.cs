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

using System;
using Hyperledger.Fabric.Tests.Helper;
using Hyperledger.Fabric_CA.SDK.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK_CA.Exception
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class FabricCAExceptionsTest
    {
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "test")]
        public void TestEnrollmentException1()
        {
            throw new EnrollmentException("test");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(EnrollmentException), "test")]
        public void TestEnrollmentException2()
        {
            throw new EnrollmentException("test", new EnrollmentException("test"));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "test")]
        public void TestInvalidIllegalArgumentException1()
        {
            throw new ArgumentException("test");
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "test")]
        public void TestInvalidIllegalArgumentException2()
        {
            throw new ArgumentException("test", new ArgumentException("test"));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(ArgumentException), "test")]
        public void TestInvalidIllegalArgumentException3()
        {
            throw new ArgumentException("test", new ArgumentException("test"));
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(RegistrationException), "test")]
        public void TestRegistrationException()
        {
            System.Exception baseException = new System.Exception("test");

            throw new RegistrationException("test", baseException);
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(RevocationException), "test")]
        public void TestRevocationException()
        {
            System.Exception baseException = new System.Exception("test");

            throw new RevocationException("test", baseException);
        }
    }
}