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

/**
 * Factory to produce a set of crypto suite implementations offering differing cryptographic algorithms and strengths.
 */

using System.Collections.Generic;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Security
{
    public interface ICryptoSuiteFactory
    {

        /**
         * If set as the default security provider then default crypto suite will not use explicit
         * provider
         */

        //string DEFAULT_JDK_PROVIDER = "org.hyperledger.fabric.sdk.security.default_jdk_provider";

        /**
         * Produce a crypto suite by specified by these properties.
         * Properties are unique to each Crypto Suite implementation.
         *
         * @param properties
         * @return
         * @throws CryptoException
         * @throws InvalidIllegalArgumentException
         */

        ICryptoSuite GetCryptoSuite(Properties properties);

        /**
         * Return a default crypto suite
         * @return
         * @throws CryptoException
         * @throws InvalidIllegalArgumentException
         */

        ICryptoSuite GetCryptoSuite();

        /**
         * This will return the default Crypto Suite Factory implementation.
         * Can be overwritten by org.hyperledger.fabric.sdk.crypto.default_crypto_suite_factory property.
         * see {@link Config#getDefaultCryptoSuiteFactory()}
         * Classes specified by this property must implement a public static method <b>instance</b> that
         * returns back a single instance of this factory.
         *
         * @return A single instance of a CryptoSuiteFactory.
         * @throws ClassNotFoundException
         * @throws IllegalAccessException
         * @throws InstantiationException
         * @throws NoSuchMethodException
         * @throws InvocationTargetException
         */
         /*
        static CryptoSuiteFactory getDefault() {

            return HLSDKJCryptoSuiteFactory.getDefault();

        }*/
    }
}