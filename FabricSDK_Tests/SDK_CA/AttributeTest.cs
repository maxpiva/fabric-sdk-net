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


using Hyperledger.Fabric_CA.SDK;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Hyperledger.Fabric.Tests.SDK_CA
{
    [TestClass]
    [TestCategory("SDK_CA")]
    public class AttributeTest
    {
        private static readonly string attrName = "some name";
        private static readonly string attrValue = "some value";

        [TestMethod]
        public void TestNewInstance()
        {
            try
            {
                Attribute testAttribute = new Attribute(attrName, attrValue);
                Assert.IsNotNull(testAttribute.Name);
                Assert.AreSame(testAttribute.Name, attrName);
                Assert.IsNotNull(testAttribute.Value);
                Assert.AreSame(testAttribute.Value, attrValue);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }

        [TestMethod]
        public void TestJsonBuild()
        {
            try
            {
                Attribute testAttribute = new Attribute(attrName, attrValue);
                JObject attrJson = testAttribute.ToJsonObject();
                Assert.IsNotNull(attrJson);
                Assert.AreEqual(attrJson["name"].Value<string>(), attrName);
                Assert.AreEqual(attrJson["value"].Value<string>(), attrValue);
            }
            catch (System.Exception e)
            {
                Assert.Fail($"Unexpected Exception {e.Message}");
            }
        }
    }
}