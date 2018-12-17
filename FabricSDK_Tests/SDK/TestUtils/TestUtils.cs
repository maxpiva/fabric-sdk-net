/*
 *
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Google.Protobuf;
using Hyperledger.Fabric.Protos.Msp;
using Hyperledger.Fabric.SDK;
using Hyperledger.Fabric.SDK.Channels;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Identity;
using Hyperledger.Fabric.SDK.Security;
using Hyperledger.Fabric.Tests.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Hyperledger.Fabric.Tests.SDK.TestUtils
{
    public class TestUtils
    {
        //Reflection methods deleted, there is no need, stuff marked as internal
        private static readonly string MOCK_CERT = string.Join("\r\n", "-----BEGIN CERTIFICATE-----", "MIICGjCCAcCgAwIBAgIRAPDmqtljAyXFJ06ZnQjXqbMwCgYIKoZIzj0EAwIwczEL", "MAkGA1UEBhMCVVMxEzARBgNVBAgTCkNhbGlmb3JuaWExFjAUBgNVBAcTDVNhbiBG", "cmFuY2lzY28xGTAXBgNVBAoTEG9yZzEuZXhhbXBsZS5jb20xHDAaBgNVBAMTE2Nh", "Lm9yZzEuZXhhbXBsZS5jb20wHhcNMTcwNjIyMTIwODQyWhcNMjcwNjIwMTIwODQy", "WjBbMQswCQYDVQQGEwJVUzETMBEGA1UECBMKQ2FsaWZvcm5pYTEWMBQGA1UEBxMN", "U2FuIEZyYW5jaXNjbzEfMB0GA1UEAwwWQWRtaW5Ab3JnMS5leGFtcGxlLmNvbTBZ", "MBMGByqGSM49AgEGCCqGSM49AwEHA0IABJve76Fj5T8Vm+FgM3p3TwcnW/npQlTL", "P+fY0fImBODqQLTkBokx4YiKcQXQl4m1EM1VAbOhAlBiOfNRNL0W8aGjTTBLMA4G", "A1UdDwEB/wQEAwIHgDAMBgNVHRMBAf8EAjAAMCsGA1UdIwQkMCKAIPz3drAqBWAE", "CNC+nZdSr8WfZJULchyss2O1uVoP6mIWMAoGCCqGSM49BAMCA0gAMEUCIQDatF1P", "L7SavLsmjbFxdeVvLnDPJuCFaAdr88oE2YuAvwIgDM4qXAcDw/AhyQblWR4F4kkU", "NHvr441QC85U+V4UQWY=", "-----END CERTIFICATE-----");

        private TestUtils()
        {
        }

        /**
 * Reset config.
 */
        public static void ResetConfig()
        {
            try
            {
                Config.config = null;
                Config _ = Config.Instance;
            }
            catch (System.Exception e)
            {
                throw new System.Exception("Cannot reset config", e);
            }
        }

        public static MockUser GetMockUser(string name, string mspId)
        {
            return new MockUser(name, mspId);
        }

        public static IEnrollment GetMockEnrollment(string cert)
        {
            return new X509Enrollment(Factory.Instance.GetCryptoSuite().KeyGen(), cert);
        }

        public static MockSigningIdentity getMockSigningIdentity(string cert, string mspId, IEnrollment enrollment)
        {
            return new MockSigningIdentity(cert, mspId, enrollment);
        }

        public static IEnrollment GetMockEnrollment(KeyPair key, string cert)
        {
            return new X509Enrollment(key, cert);
        }

        /**
    * Just for testing remove all peers and orderers and add them back.
    *
    * @param client
    * @param channel
    */
        public static void TestRemovingAddingPeersOrderers(HFClient client, Channel channel)
        {
            Dictionary<Peer, PeerOptions> perm = new Dictionary<Peer, PeerOptions>();

            Assert.IsTrue(channel.IsInitialized);
            Assert.IsFalse(channel.IsShutdown);
            Thread.Sleep(1500); // time needed let channel get config block

            channel.Peers.ToList().ForEach(peer =>
            {
                perm[peer] = channel.GetPeersOptions(peer);
                channel.RemovePeer(peer);
            });

            perm.Keys.ToList().ForEach(peer =>
            {
                PeerOptions value = perm[peer];
                Peer newPeer = client.NewPeer(peer.Name, peer.Url, peer.Properties);
                channel.AddPeer(newPeer, value);
            });

            List<Orderer> removedOrders = new List<Orderer>();

            foreach (Orderer orderer in channel.Orderers.ToList())
            {
                channel.RemoveOrderer(orderer);
                removedOrders.Add(orderer);
            }

            removedOrders.ForEach(orderer =>
            {
                Orderer newOrderer = client.NewOrderer(orderer.Name, orderer.Url, orderer.Properties);
                channel.AddOrderer(newOrderer);
            });
        }

        public static List<string> TarBytesToEntryArrayList(byte[] bytes)
        {
            List<string> ret = new List<string>();
            using (MemoryStream bos = new MemoryStream(bytes))
            using (var reader = ReaderFactory.Open(bos))
            {
                while (reader.MoveToNextEntry())
                {
                    IEntry ta = reader.Entry;
                    Assert.IsTrue(!ta.IsDirectory, $"Tar entry {ta.Key} is not a file.");
                    ret.Add(ta.Key);
                }

                return ret;
            }
            /*
        public static void AssertArrayListEquals(string failmsg, List<string> expect, List<string> actual) {
            ArrayList expectSort = new ArrayList(expect);
            Collections.sort(expectSort);
            ArrayList actualSort = new ArrayList(actual);
            Collections.sort(actualSort);
            Assert.assertArrayEquals(failmsg, expectSort.toArray(), actualSort.toArray());
        }
    
        public static Matcher<String> matchesRegex(final String regex) {
            return new TypeSafeMatcher<String>() {
                @Override
                public void describeTo(Description description) {
    
                }
    
                @Override
                protected boolean matchesSafely(final String item) {
                    return item.matches(regex);
                }
            };
        }
        */


            //  This is the private key for the above cert. Right now we don't need this and there's some class loader issues doing this here.

//    private static final String MOCK_NOT_SO_PRIVATE_KEY = "-----BEGIN PRIVATE KEY-----\n" +
//            "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQghnA7rdgbZi/wndus\n" +
//            "iXjyf0KgE6OKZjQ+5INjwelRAC6hRANCAASb3u+hY+U/FZvhYDN6d08HJ1v56UJU\n" +
//            "yz/n2NHyJgTg6kC05AaJMeGIinEF0JeJtRDNVQGzoQJQYjnzUTS9FvGh\n" +
//            "-----END PRIVATE KEY-----";

//    private static final  PrivateKey mockNotSoPrivateKey = getPrivateKeyFromBytes(MOCK_NOT_SO_PRIVATE_KEY.getBytes(StandardCharsets.UTF_8));
//
//    static PrivateKey getPrivateKeyFromBytes(byte[] data) {
//        try {
//            final Reader pemReader = new StringReader(new String(data));
//
//            final PrivateKeyInfo pemPair;
//            try (PEMParser pemParser = new PEMParser(pemReader)) {
//                pemPair = (PrivateKeyInfo) pemParser.readObject();
//            }
//
//            return new JcaPEMKeyConverter().setProvider(BouncyCastleProvider.PROVIDER_NAME).getPrivateKey(pemPair);
//        } catch (IOException e) {
//            throw new RuntimeException(e);
//        }
//    }
        }

        public static string RelocateFilePathsJSON(string filename)
        {
            return RelocateFilePaths(filename, ".json", "\"path\":\\s?\"(.*?)\"");
        }

        public static string RelocateFilePathsYAML(string filename)
        {
            return RelocateFilePaths(filename, ".yaml", "path:\\s?(.*?)$");
        }

        public static string RelocateFilePaths(string filename, string ext, string regex)
        {
            string tempfile = Path.GetTempFileName() + ext;
            string json = File.ReadAllText(filename);
            MatchCollection matches = new Regex(regex, RegexOptions.Compiled | RegexOptions.Multiline).Matches(json);
            foreach (Match m in matches)
            {
                if (m.Success)
                {
                    bool replace = false;
                    string path = m.Groups[1].Value.Replace("\r", string.Empty).Replace("\n", string.Empty);
                    if (path.StartsWith("\"") && path.EndsWith("\""))
                        path = path.Substring(1, path.Length - 2);
                    string orgpath = path;
                    if (path.StartsWith("/"))
                        path = path.Substring(1);
                    if (path.StartsWith("src/test"))
                    {
                        replace = true;
                        path = path.Substring(9);
                    }

                    if (replace)
                    {
                        path = path.Locate().Replace("\\", "/");
                        json = json.Replace(orgpath, path);
                    }
                }
            }

            File.WriteAllText(tempfile, json);
            return tempfile;
        }


        public class MockUser : IUser
        {
            public MockUser(string name, string mspId)
            {
                Name = name;
                MspId = mspId;
                Enrollment = GetMockEnrollment(MOCK_CERT);
                Roles = null;
                Account = null;
                Affiliation = null;
            }

            public string EnrollmentSecret { get; set; }

            public string Name { get; }
            public HashSet<string> Roles { get; }
            public string Account { get; }
            public string Affiliation { get; }
            public IEnrollment Enrollment { get; set; }
            public string MspId { get; }
        }

        public class MockSigningIdentity : ISigningIdentity
        {
            public MockSigningIdentity(string cert, string mspId, IEnrollment enrollment)
            {
                Cert = cert;
                MspId = mspId;
                Enrollment = enrollment;
            }

            private string Cert { get; }
            public IEnrollment Enrollment { get; set; }
            public string MspId { get; }


            public byte[] Sign(byte[] msg)
            {
                try
                {
                    return Factory.GetCryptoSuite().Sign(Enrollment.GetKeyPair(), msg);
                }
                catch (System.Exception e)
                {
                    throw new CryptoException(e.Message, e);
                }
            }

            public bool VerifySignature(byte[] msg, byte[] sig)
            {
                return false;
            }


            public SerializedIdentity CreateSerializedIdentity()
            {
                return new SerializedIdentity {IdBytes = ByteString.CopyFromUtf8(Cert), Mspid = MspId};
            }
        }
    }
}