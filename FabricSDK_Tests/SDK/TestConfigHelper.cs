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

using System;
using System.Collections.Generic;
using System.Reflection;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.Tests.SDK
{
/* Container for methods to set SDK environment before running unit+integration tests * */

public class TestConfigHelper
{
    public static readonly string CONFIG_OVERRIDES = "FABRICSDKOVERRIDES";

    /**
     * clearConfig "resets" Config so that the Config testcases can run without interference from other test suites.
     * Depending on what order JUnit decides to run the tests, Config could have been instantiated earlier and could
     * contain values that make the tests here fail.
     *
     * @throws SecurityException
     * @throws NoSuchFieldException
     * @throws IllegalAccessException
     * @throws IllegalArgumentException
     *
     */
    public void ClearConfig()
    {
        Config.config = null;
        Config.sdkProperties = null;
    }

    /**
     * clearCaConfig "resets" Config used by fabric_ca so that the Config testcases can run without interference from
     * other test suites.
     *
     * @throws SecurityException
     * @throws NoSuchFieldException
     * @throws IllegalAccessException
     * @throws IllegalArgumentException
     *
     * @see #clearConfig()
     */
    public void ClearCaConfig()
    {
        Fabric_CA.SDK.Helper.Config.config = null;
        Fabric_CA.SDK.Helper.Config.sdkProperties = null;

    }

    /**
     * customizeConfig() sets up the properties listed by env var CONFIG_OVERRIDES The value of the env var is
     * <i>property1=value1,property2=value2</i> and so on where each <i>property</i> is a property from the SDK's config
     * file.
     *
     * @throws NoSuchFieldException
     * @throws SecurityException
     * @throws IllegalArgumentException
     * @throws IllegalAccessException
     */
    public void CustomizeConfig()
    {
        string fabricSdkConfig = Environment.GetEnvironmentVariable(CONFIG_OVERRIDES);
        if (!string.IsNullOrEmpty(fabricSdkConfig))
        {
            string[] configs = fabricSdkConfig.Split(new char[] {','});
            string[] configKeyValue;
            foreach (string config in configs)
            {
                configKeyValue = config.Split(new char[] {'='});
                if (configKeyValue.Length == 2)
                {
                    Environment.SetEnvironmentVariable(configKeyValue[0], configKeyValue[1]);
                }
            }
        }
    }
}