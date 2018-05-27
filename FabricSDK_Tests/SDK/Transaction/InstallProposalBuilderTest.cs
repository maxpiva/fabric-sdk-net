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
using System.IO;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Builders;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Requests;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Transaction
{
    [TestClass]
    [TestCategory("SDK")]
    public class InstallProposalBuilderTest
    {
        // Create a temp folder to hold temp files for various file I/O operations
        // These are automatically deleted when each test completes
        public readonly string tempFolder = Path.GetTempPath();

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "Missing chaincodeSource or chaincodeInputStream")]
        public void TestBuildNoChaincode()
        {
            InstallProposalBuilder builder = CreateTestBuilder();
            builder.Build();
        }





        // Tests that both chaincodeSource and chaincodeInputStream are not specified together
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "Both chaincodeSource and chaincodeInputStream")]
        public void TestBuildBothChaincodeSources()
        {
            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeSource(new DirectoryInfo("some/dir"));
            builder.SetChaincodeInputStream(new MemoryStream("test string".ToBytes()));
            builder.Build();
        }

        // Tests that a chaincode path has been specified for GO_LANG code using a File
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "Missing chaincodePath")]
        public void TestBuildChaincodePathGolangFile()
        {
            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeLanguage(TransactionRequest.Type.GO_LANG);
            builder.ChaincodeSource(new DirectoryInfo("some/dir"));
            builder.ChaincodePath(null);

            builder.Build();
        }

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "Missing chaincodePath")]
        public void TestBuildChaincodePathGolangStream()
        {
            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeLanguage(TransactionRequest.Type.GO_LANG);
            builder.SetChaincodeInputStream(new MemoryStream("test string".ToBytes()));
            builder.ChaincodePath(null);

            builder.Build();
        }

        // Tests that a chaincode path is null for JAVA code using a File

        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "chaincodePath must be null for Java chaincode")]
        public void TestBuildChaincodePathJavaFile()
        {
            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeLanguage(TransactionRequest.Type.JAVA);
            builder.ChaincodeSource(new DirectoryInfo("some/dir"));
            builder.ChaincodePath("null or empty string");

            builder.Build();
        }

        // Tests that a chaincode path is null for JAVA code using a File
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "chaincodePath must be null for Java chaincode")]
        public void TestBuildChaincodePathJavaStream()
        {
            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeLanguage(TransactionRequest.Type.JAVA);
            builder.SetChaincodeInputStream(new MemoryStream("test string".ToBytes()));
            builder.ChaincodePath("null or empty string");

            builder.Build();
        }

        // Tests for non existent chaincode source path
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "The project source directory does not exist")]
        public void TestBuildSourceNotExistGolang()
        {
            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeLanguage(TransactionRequest.Type.JAVA);
            builder.ChaincodePath(null);
            builder.ChaincodeSource(new DirectoryInfo("some/dir"));

            builder.Build();
        }

        // Tests for a chaincode source path which is a file and not a directory
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "The project source directory is not a directory")]
        public void TestBuildSourceNotDirectory()
        {
            InstallProposalBuilder builder = CreateTestBuilder();
            string folderpath = Path.Combine(tempFolder, "src");
            // create an empty src directory
            Directory.CreateDirectory(folderpath);
            // Create a dummy file in the chaincode directory
            string dummyFileName = "myapp";
            string filepath = Path.Combine(folderpath, dummyFileName);
            File.WriteAllText(filepath, string.Empty);

            builder.ChaincodePath(folderpath);
            builder.ChaincodeSource(new DirectoryInfo(filepath));

            builder.Build();
        }

        // Tests for an IOException on the stream
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "IO Error")]
        public void TestBuildInvalidSource()
        {
            // A mock InputStream that throws an IOException


            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeLanguage(TransactionRequest.Type.JAVA);
            builder.SetChaincodeInputStream(new MockInputStream());

            builder.Build();
        }

        // Tests that no chaincode path is specified for Node code using a File
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "chaincodePath must be null for Node chaincode")]
        public void TestBuildChaincodePathNodeFile()
        {
            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeLanguage(TransactionRequest.Type.NODE);
            builder.ChaincodeSource(new DirectoryInfo("some/dir"));
            builder.ChaincodePath("src");

            builder.Build();
        }

        // Tests that no chaincode path is specified for Node code using input stream
        [TestMethod]
        [ExpectedExceptionWithMessage(typeof(IllegalArgumentException), "chaincodePath must be null for Node chaincode")]
        public void TestBuildChaincodePathNodeStream()
        {
            InstallProposalBuilder builder = CreateTestBuilder();

            builder.ChaincodeLanguage(TransactionRequest.Type.NODE);
            builder.SetChaincodeInputStream(new MemoryStream("test string".ToBytes()));
            builder.ChaincodePath("src");

            builder.Build();
        }

        public  class MockInputStream : MemoryStream
        {
            public MockInputStream()
            {
            }

            public MockInputStream(int capacity) : base(capacity)
            {
            }

            public MockInputStream(byte[] buffer) : base(buffer)
            {
            }

            public MockInputStream(byte[] buffer, bool writable) : base(buffer, writable)
            {
            }

            public MockInputStream(byte[] buffer, int index, int count) : base(buffer, index, count)
            {
            }

            public MockInputStream(byte[] buffer, int index, int count, bool writable) : base(buffer, index, count, writable)
            {
            }

            public MockInputStream(byte[] buffer, int index, int count, bool writable, bool publiclyVisible) : base(buffer, index, count, writable, publiclyVisible)
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new IOException("Cannot read!");
            }
        }
        // ==========================================================================================
        // Helper methods
        // ==========================================================================================

        // Instantiates a basic InstallProposalBuilder with no chaincode source specified
        private InstallProposalBuilder CreateTestBuilder()
        {
            InstallProposalBuilder builder = InstallProposalBuilder.Create();
            builder.ChaincodeName("mycc");
            builder.ChaincodeVersion("1.0");
            builder.ChaincodeLanguage(TransactionRequest.Type.GO_LANG);

            return builder;
        }
    }
}