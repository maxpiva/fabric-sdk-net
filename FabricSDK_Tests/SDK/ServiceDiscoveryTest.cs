using System;
using System.Collections.Generic;
using Hyperledger.Fabric.SDK.Discovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;
// ReSharper disable VirtualMemberCallInConstructor

namespace Hyperledger.Fabric.Tests.SDK
{
    [TestClass]
    [TestCategory("SDK")]
    public class ServiceDiscoveryTest
    {
        private static List<SDEndorser> FilterByEndpoint(List<SDEndorser> sdEndorsers, string needle)
        {
            List<SDEndorser> ret = new List<SDEndorser>();
            sdEndorsers.ForEach(sdEndorser =>
            {
                if (sdEndorser.Endpoint.Contains(needle))
                {
                    ret.Add(sdEndorser);
                }
            });

            return ret;
        }

        [TestMethod]
        public void SimpleOneEach()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_LEAST_REQUIRED_BLOCKHEIGHT;

            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdLayout.AddGroup("G1", 1, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:91", 20));
            sdLayout.AddGroup("G2", 1, sdl);
            lol.Add(sdLayout);
            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            SDEndorserState sdEndorserState = es(cc);
            List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;
            Assert.AreEqual(2, sdEndorsers.Count);
            Assert.IsTrue(sdLayout == sdEndorserState.PickedLayout);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "localhost").Count, 1);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "otherhost").Count, 1);
        }

        [TestMethod]
        public void SimpleOneTwoEach()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_LEAST_REQUIRED_BLOCKHEIGHT;
            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdLayout.AddGroup("G1", 1, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:91", 20));
            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);
            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            SDEndorserState sdEndorserState = es(cc);
            List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;
            Assert.AreEqual(3, sdEndorsers.Count);
            Assert.IsTrue(sdLayout == sdEndorserState.PickedLayout);

            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "localhost").Count, 1);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "otherhost").Count, 2);
        }

        [TestMethod]
        public void SimpleTwoTwoEach()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_LEAST_REQUIRED_BLOCKHEIGHT;

            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdLayout.AddGroup("G1", 2, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:91", 20));
            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);
            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            SDEndorserState sdEndorserState = es(cc);
            List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;
            Assert.AreEqual(4, sdEndorsers.Count);
            Assert.IsTrue(sdLayout == sdEndorserState.PickedLayout);

            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "localhost").Count, 2);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "otherhost").Count, 2);
        }

        [TestMethod]
        public void SimpleTwoTwoEachExtras()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_LEAST_REQUIRED_BLOCKHEIGHT;
            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:82", 20));
            sdLayout.AddGroup("G1", 2, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:93", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:91", 20));

            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);
            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            SDEndorserState sdEndorserState = es(cc);
            List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;
            Assert.AreEqual(4, sdEndorsers.Count);
            Assert.IsTrue(sdLayout == sdEndorserState.PickedLayout);

            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "localhost").Count, 2);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "otherhost").Count, 2);
        }

        [TestMethod]
        public void simpleTwoTwoEachExtrasCommon()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_LEAST_REQUIRED_BLOCKHEIGHT;

            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdl.Add(new MockSDEndorser("org1", "commonHost:82", 20));
            sdLayout.AddGroup("G1", 2, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:93", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org1", "commonHost:82", 20)); // << the same

            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);
            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            SDEndorserState sdEndorserState = es(cc);
            List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;
            Assert.AreEqual(3, sdEndorsers.Count);
            Assert.IsTrue(sdLayout == sdEndorserState.PickedLayout);

            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "localhost").Count, 1);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "otherhost").Count, 1);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "commonHost").Count, 1);
        }

        [TestMethod]
        public void twoLayoutTwoTwoEachExtrasCommon()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_LEAST_REQUIRED_BLOCKHEIGHT;

            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:83", 20));
            sdLayout.AddGroup("G1", 3, sdl); // << 3 needed
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:93", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org1", "commonHost:82", 20));

            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);

            sdLayout = new SDLayout(); // another layout the above needs 3
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "l2localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "l2localhost:81", 20));
            sdl.Add(new MockSDEndorser("org1", "l2commonHost:82", 20));
            sdLayout.AddGroup("G1", 2, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "l2otherhost:93", 20));
            sdl.Add(new MockSDEndorser("org2", "l2otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org1", "l2commonHost:82", 20));

            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);

            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            SDEndorserState sdEndorserState = es(cc);
            List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;
            Assert.AreEqual(3, sdEndorsers.Count);
            Assert.IsTrue(sdLayout == sdEndorserState.PickedLayout);

            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "l2localhost").Count, 1);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "l2otherhost").Count, 1);
            Assert.AreEqual(FilterByEndpoint(sdEndorsers, "l2commonHost").Count, 1);
        }

        [TestMethod]
        public void simpleOneEachRandom()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_RANDOM;

            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdLayout.AddGroup("G1", 1, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:91", 20));
            sdLayout.AddGroup("G2", 1, sdl);
            lol.Add(sdLayout);
            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            SDEndorserState sdEndorserState = es(cc);
            for (int i = 64; i > 0; --i)
            {
                List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;
                Assert.AreEqual(2, sdEndorsers.Count);
                Assert.IsTrue(sdLayout == sdEndorserState.PickedLayout);

                Assert.AreEqual(FilterByEndpoint(sdEndorsers, "localhost").Count, 1);
                Assert.AreEqual(FilterByEndpoint(sdEndorsers, "otherhost").Count, 1);
            }
        }

        [TestMethod]
        public void simpleOneTwoEachRandom()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_RANDOM;

            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdLayout.AddGroup("G1", 1, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:91", 20));
            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);
            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            for (int i = 64; i > 0; --i)
            {
                SDEndorserState sdEndorserState = es(cc);
                List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;
                Assert.AreEqual(3, sdEndorsers.Count);
                Assert.IsTrue(sdLayout == sdEndorserState.PickedLayout);

                Assert.AreEqual(FilterByEndpoint(sdEndorsers, "localhost").Count, 1);
                Assert.AreEqual(FilterByEndpoint(sdEndorsers, "otherhost").Count, 2);
            }
        }

        [TestMethod]
        public void twoLayoutTwoTwoEachExtrasCommonRandom()
        {
            Func<SDChaindcode, SDEndorserState> es = ServiceDiscovery.ENDORSEMENT_SELECTION_RANDOM;

            List<SDLayout> lol = new List<SDLayout>();
            SDLayout sdLayout = new SDLayout();
            List<SDEndorser> sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:81", 20));
            sdl.Add(new MockSDEndorser("org1", "localhost:83", 20));
            sdLayout.AddGroup("G1", 3, sdl); // << 3 needed
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "otherhost:93", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org2", "otherhost:82", 20));

            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);

            sdLayout = new SDLayout(); // another layout the above needs 3
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org1", "l2localhost:80", 20));
            sdl.Add(new MockSDEndorser("org1", "l2localhost:81", 20));
            sdl.Add(new MockSDEndorser("org1", "l2localhost:82", 20));
            sdLayout.AddGroup("G1", 1, sdl);
            sdl = new List<SDEndorser>();
            sdl.Add(new MockSDEndorser("org2", "l2otherhost:93", 20));
            sdl.Add(new MockSDEndorser("org2", "l2otherhost:90", 20));
            sdl.Add(new MockSDEndorser("org1", "l2otherhost:82", 20));

            sdLayout.AddGroup("G2", 2, sdl);
            lol.Add(sdLayout);

            SDChaindcode cc = new SDChaindcode("fakecc", lol);
            for (int i = 64; i > 0; --i)
            {
                SDEndorserState sdEndorserState = es(cc);
                List<SDEndorser> sdEndorsers = sdEndorserState.SDEndorsers;

                Assert.IsTrue(FilterByEndpoint(sdEndorsers, "localhost").Count == 3 && FilterByEndpoint(sdEndorsers, "otherhost").Count == 2 || FilterByEndpoint(sdEndorsers, "l2localhost").Count == 1 && FilterByEndpoint(sdEndorsers, "l2otherhost").Count == 2);
            }
        }

        private class MockSDEndorser : SDEndorser
        {
            public MockSDEndorser(string mspid, string endpoint, long ledgerHeight)
            {
                Endpoint = endpoint;
                MspId = mspid;
                LedgerHeight = ledgerHeight;
            }
        }
    }
}