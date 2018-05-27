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

using System;
using Google.Protobuf.WellKnownTypes;
using Hyperledger.Fabric.SDK.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hyperledger.Fabric.Tests.SDK.Transaction
{
    [TestClass]
    [TestCategory("SDK")]
    public class ProtoUtilsTest
    {
        [TestMethod]
        public void TimeStampDrill()
        {
            long millis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            //Test values over 2seconds
            for (long start = millis; start < millis + 2010; ++start)
            {
                Timestamp ts = new Timestamp {Seconds = start / 1000, Nanos = (int) (start % 1000 * 1000000)};

                DateTime dateFromTimestamp = ts.ToDateTime();
                //    System.out.println(dateFromTimestamp);
                DateTime expectedDate = new DateTime(start);
                //Test various formats to make sure...
                Assert.AreEqual(expectedDate, dateFromTimestamp);
                Assert.AreEqual(expectedDate.TimeOfDay, dateFromTimestamp.TimeOfDay);
                Assert.AreEqual(expectedDate.ToString(), dateFromTimestamp.ToString());
                //Now reverse it
                Timestamp timestampFromDate = expectedDate.ToTimestamp();
                Assert.AreEqual(ts, timestampFromDate);
                Assert.AreEqual(ts.Nanos, timestampFromDate.Nanos);
                Assert.AreEqual(ts.Seconds, timestampFromDate.Seconds);
                Assert.AreEqual(ts.ToString(), timestampFromDate.ToString());
            }
        }

        [TestMethod]
        public void TimeStampCurrent()
        {
            int skew = 200; // need some skew here as we are not getting the times at same instance.
            DateTime original = DateTime.UtcNow;
            DateTime currentDateTimestamp = ProtoUtils.GetCurrentFabricTimestamp().ToDateTime();
            DateTime before = original.AddMilliseconds(-skew);
            DateTime after = original.AddMilliseconds(skew);
            Assert.IsTrue(before < currentDateTimestamp);
            Assert.IsTrue(after > currentDateTimestamp);
        }
    }
}