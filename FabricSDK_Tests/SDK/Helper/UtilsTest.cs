/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology - All Rights Reserved.
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
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using Hyperledger.Fabric.SDK.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto.Digests;
using Utils = Hyperledger.Fabric.SDK.Helper.Utils;

namespace Hyperledger.Fabric.Tests.SDK.Helper
{
    public class UtilsTest
    {
        private static readonly string SAMPLE_GO_CC = "fixture/sdkintegration/gocc/sample1";
        // Create a temp folder to hold temp files for various file I/O operations
        // These are automatically deleted when each test completes

        public readonly string tempFolder = Path.GetTempPath();

        [TestMethod]
        public void TestGenerateParameterHash()
        {
            List<string> args = new List<string>();
            args.Add("a");
            args.Add("b");
            string hash = Utils.GenerateParameterHash("mypath", "myfunc", args);
            Assert.AreEqual(Utils.Hash("mypathmyfuncab".ToBytes(), new Sha3Digest()).ToHexString(), hash);
        }

        // Tests generateDirectoryHash passing it a null rootDir and no previous hash
        [TestMethod]
        public void TestGenerateDirectoryHash()
        {
            DoGenerateDirectoryHash(false, false);
        }

        // Tests generateDirectoryHash passing it a rootDir and no previous hash
        [TestMethod]
        public void TestGenerateDirectoryHashWithRootDir()
        {
            DoGenerateDirectoryHash(true, false);
        }

        // Tests generateDirectoryHash passing it a previous hash
        [TestMethod]
        public void TestGenerateDirectoryHashWithPrevHash()
        {
            DoGenerateDirectoryHash(true, true);
        }

        // Test generateDirectoryHash with a non-existent directory
        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestGenerateDirectoryHashNoDirectory()
        {
            DirectoryInfo rootDir = new DirectoryInfo(tempFolder);
            DirectoryInfo nonExistentDir = new DirectoryInfo(Path.Combine(rootDir.FullName, "temp"));

            Utils.GenerateDirectoryHash(null, nonExistentDir.FullName, "");
            Assert.Fail("Expected an IOException as the directory does not exist");
        }

        // Test generateDirectoryHash with an empty directory
        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestGenerateDirectoryHashEmptyDirectory()
        {
            DirectoryInfo temp = new DirectoryInfo(tempFolder);
            DirectoryInfo emptyDir = temp.CreateSubdirectory("subfolder");

            Utils.GenerateDirectoryHash(null, emptyDir.FullName, "");
            Assert.Fail("Expected an IOException as the directory is empty");
        }

        // Test generateDirectoryHash by passing it a file
        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestGenerateDirectoryHashWithFile()
        {
            // Create a temp file
            string tempfile = Path.Combine(tempFolder, "temp.txt");
            File.OpenWrite(tempfile).Close();
            Utils.GenerateDirectoryHash(null, tempfile, "");
            Assert.Fail("Expected an IOException as we passed it a file");
        }


        // Tests deleting a file
        [TestMethod]
        public void TestDeleteFileOrDirectoryFile()
        {
            string tempfile = Path.Combine(tempFolder, "temp.txt");
            File.OpenWrite(tempfile).Close();
            Assert.IsTrue(File.Exists(tempfile));
            Utils.DeleteFileOrDirectory(tempfile);
            Assert.IsFalse(File.Exists(tempfile));
        }

        // Tests deleting a directory
        [TestMethod]
        public void TestDeleteFileOrDirectoryDirectory()
        {
            // create a temp directory with some files in it
            DirectoryInfo tempDir = CreateTempDirWithFiles();

            // Ensure the dir exists
            Assert.IsTrue(tempDir.Exists);

            Utils.DeleteFileOrDirectory(tempDir.FullName);

            // Ensure the file was deleted
            Assert.IsFalse(tempDir.Exists);
        }

        // Test compressing a directory
        [TestMethod]
        public void TestGenerateTarGz()
        {
            // create a temp directory with some files in it
            DirectoryInfo tempDir = CreateTempDirWithFiles();

            // Compress
            byte[] data = Utils.GenerateTarGz(tempDir.FullName, "newPath", null);

            // Here, we simply ensure that it did something!
            Assert.IsNotNull(data);
            Assert.IsTrue(data.Length > 0);
        }

        // Test compressing an empty directory
        [TestMethod]
        public void TestGenerateTarGzEmptyDirectory()
        {
            // create an empty directory
            DirectoryInfo emptyDir = new DirectoryInfo(tempFolder).CreateSubdirectory("subfolder");
            byte[] data = Utils.GenerateTarGz(emptyDir.FullName, null, null);

            // Here, we simply ensure that it did something!
            Assert.IsNotNull(data);
            Assert.IsTrue(data.Length > 0);
        }

        // Test compressing a non-existent directory
        // Note that this currently throws an IllegalArgumentException, and not an IOException!
        [TestMethod]
        [ExpectedException(typeof(IOException))]
        public void TestGenerateTarGzNoDirectory()
        {
            string nonexistantdir = Path.Combine(tempFolder, "temp");
            Utils.GenerateTarGz(nonexistantdir, null, null);
            Assert.Fail("Expected an IOException as the directory does not exist");
        }

        [TestMethod]
        public void TestGenerateTarGzMETAINF()
        {
            List<string> expect = new List<string> {"META-INF/statedb/couchdb/indexes/MockFakeIndex.json", "src/github.com/example_cc/example_cc.go"};
            expect.Sort();
            string path = Path.GetFullPath(Path.Combine(SAMPLE_GO_CC, "/src/github.com/example_cc"));
            string metainf = Path.GetFullPath("fixture/meta-infs/test1/META-INF");
            byte[] bytes = Utils.GenerateTarGz(path, "src/github.com/example_cc", metainf);
            Assert.IsNotNull(bytes, "generateTarGz() returned null bytes.");
            List<string> tarBytesToEntryArrayList = TestUtils.TestUtils.TarBytesToEntryArrayList(bytes);
            CollectionAssert.AreEquivalent(expect, tarBytesToEntryArrayList, "Tar not what expected.");
        }

        [TestMethod]
        public void TestGenerateTarGzNOMETAINF()
        {
            List<string> expect = new List<string> {"src/github.com/example_cc/example_cc.go"};

            string path = Path.GetFullPath(Path.Combine(SAMPLE_GO_CC, "/src/github.com/example_cc"));

            byte[] bytes = Utils.GenerateTarGz(path, "src/github.com/", null);
            Assert.IsNotNull(bytes, "generateTarGz() returned null bytes.");
            List<string> tarBytesToEntryArrayList = TestUtils.TestUtils.TarBytesToEntryArrayList(bytes);
            CollectionAssert.AreEquivalent(expect, tarBytesToEntryArrayList, "Tar not what expected.");
        }


        [TestMethod]
        public void TestGenerateUUID()
        {
            // As this is a "unique" identifier, we call the function twice to
            // ensure it doesn't return the same value
            string uuid1 = Utils.GenerateUUID();
            string uuid2 = Utils.GenerateUUID();

            Assert.IsNotNull(uuid1);
            Assert.IsNotNull(uuid2);
            Assert.AreNotEqual(uuid1, uuid2, "gererateUUID returned a duplicate UUID!");
        }

        [TestMethod]
        public void TestGenerateNonce()
        {
            // As this is a "unique" identifier, we call the function twice to
            // ensure it doesn't return the same value
            byte[] nonce1 = Utils.GenerateNonce();
            byte[] nonce2 = Utils.GenerateNonce();

            Assert.IsNotNull(nonce1);
            Assert.IsNotNull(nonce2);
            Assert.AreNotEqual(nonce1, nonce2, "generateNonce returned a duplicate nonce!");
        }

        [TestMethod]
        public void TestGenerateTimestamp()
        {
            Assert.IsNotNull(Utils.GenerateTimestamp());
        }

        [TestMethod]
        public void TestHash()
        {
            byte[] input = "TheQuickBrownFox".ToBytes();
            string expectedHash = "feb69c5c360a15802de6af23a3f5622da9d96aff2be78c8f188cce57a3549db6";

            byte[] hash = Utils.Hash(input, new Sha3Digest());
            Assert.AreEqual(expectedHash, hash.ToHexString());
        }


        [TestMethod]
        public void TestParseGrpcUrl()
        {
            string url = "grpc://hyperledger.org:1234";

            (string Protocol, string Host, int Port) = Utils.ParseGrpcUrl(url);

            Assert.AreEqual("grpc", Protocol);
            Assert.AreEqual("hyperledger.org", Host);
            Assert.AreEqual(1234, Port);
        }

        [TestMethod]
        public void TestCheckGrpcUrlValid()
        {
            // Test a number of valid variations
            Assert.IsNull(Utils.CheckGrpcUrl("grpc://hyperledger.org:1234"));
            Assert.IsNull(Utils.CheckGrpcUrl("grpcs://127.0.0.1:1234"));
        }

        [TestMethod]
        public void testCheckGrpcUrlInvalid()
        {
            // Test a number of invalid variations
            Assert.IsNotNull(Utils.CheckGrpcUrl("http://hyperledger.org:1234"));
            Assert.IsNotNull(Utils.CheckGrpcUrl("grpc://hyperledger.org"));
            Assert.IsNotNull("grpc://hyperledger.org:1234/index.html");
        }


        [TestMethod]
        public void testLogString()
        {
            // Test a number of variations
            Assert.AreEqual(null, ((string) null).LogString());
            Assert.AreEqual("", "".LogString());
            Assert.AreEqual("ab??c", "ab\r\nc".LogString());
            Assert.AreEqual("ab?c", "ab\tc".LogString());
        }

        [TestMethod]
        public void TestToHexString()
        {
            Assert.AreEqual("414243", "ABC".ToBytes().ToHexString());
            Assert.AreEqual("41090a", "A\t\n".ToBytes().ToHexString());
        }

        [TestMethod]
        public void testToHexStringNull()
        {
            Assert.IsNull(((byte[]) null).ToHexString());
            Assert.IsNull(((ByteString) null).ToHexString());
        }

        // ==========================================================================================
        // Helper methods
        // ==========================================================================================

        // Helper method to allow tests of multiple code paths through generateDirectoryHash
        public void DoGenerateDirectoryHash(bool useRootDir, bool usePreviousHash)
        {
            // Use any old hash value
            string previousHashToUse = "3c08029b52176eacf802dee93129a9f1fd115008950e1bb968465dcd51bbbb9d";

            // The hashes expected when we 1: do not pass a previousHash and 2: pass
            // the previousHash
            string expectedHash1 = "a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a";
            string expectedHash2 = "6c9f96b2dd87d7a02fd3b7cc6026a6a96d21c4c53aaf5777439151690c48c7b8";
            string expectedHash = usePreviousHash ? expectedHash2 : expectedHash1;

            string chaincodeSubDirString = "chaincode/example/java";

            // Create the temp directories
            string rootDir = Path.GetFullPath(tempFolder);
            string chaincodeDir = Path.Combine(rootDir, chaincodeSubDirString);
            Directory.CreateDirectory(chaincodeDir);

            string rootDirString = null;
            string chaincodeDirString;

            if (useRootDir)
            {
                // Pass both a RootDir and a chaincodeDir to the function

                rootDirString = rootDir;
                chaincodeDirString = chaincodeSubDirString;
            }
            else
            {
                // Pass just a chaincodeDir to the function
                chaincodeDirString = chaincodeDir;
            }

            // Create a dummy file in the chaincode directory

            string tempfile = Path.Combine(tempFolder, "temp.txt");
            File.OpenWrite(tempfile).Close();

            string previousHash = usePreviousHash ? previousHashToUse : "";

            string hash = Utils.GenerateDirectoryHash(rootDirString, chaincodeDirString, previousHash);
            Assert.AreEqual(expectedHash, hash);
        }

        // Creates a temp directory containing a couple of files
        private DirectoryInfo CreateTempDirWithFiles()
        {
            DirectoryInfo dirinfo = new DirectoryInfo(Path.GetFullPath(tempFolder));
            // create a temp directory with some files in it
            DirectoryInfo tempDir = dirinfo.CreateSubdirectory("tempDir");
            string tempfile1 = Path.Combine(tempDir.FullName, "test1.txt");
            File.WriteAllBytes(tempfile1, "TheQuickBrownFox".ToBytes());
            string tempfile2 = Path.Combine(tempDir.FullName, "test2.txt");
            File.WriteAllBytes(tempfile2, "JumpsOverTheLazyDog".ToBytes());
            return tempDir;
        }
    }
}