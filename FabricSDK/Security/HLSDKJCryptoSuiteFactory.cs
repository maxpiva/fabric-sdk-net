/*
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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

/**
 * SDK's Default implementation of CryptoSuiteFactory.
 */

using System;
using System.Collections.Concurrent;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Security
{
    public class HLSDKJCryptoSuiteFactory : ICryptoSuiteFactory
    {
        private static readonly ConcurrentDictionary<Properties, ICryptoSuite> cache = new ConcurrentDictionary<Properties, ICryptoSuite>();
        private readonly string HASH_ALGORITHM = Config.Instance.GetHashAlgorithm();
        private readonly int SECURITY_LEVEL = Config.Instance.GetSecurityLevel();

        internal HLSDKJCryptoSuiteFactory()
        {
        }


        public ICryptoSuite GetCryptoSuite(Properties properties)
        {
            ICryptoSuite ret = null;
            foreach (Properties st in cache.Keys)
            {
                bool found = true;
                foreach (string key in properties.Keys)
                {
                    if (!st.Contains(key))
                        found = false;
                    else
                    {
                        if (st[key] != properties[key])
                            found = false;
                    }

                    if (!found)
                        break;
                }

                if (found)
                {
                    ret = cache[st];
                    break;
                }
            }

            if (ret == null)
            {
                try
                {
                    CryptoPrimitives cp = new CryptoPrimitives();
                    cp.SetProperties(properties);
                    cp.Init();
                    ret = cp;
                }
                catch (Exception e)
                {
                    throw new CryptoException(e.Message, e);
                }

                cache[properties] = ret;
            }

            return ret;
        }


        public ICryptoSuite GetCryptoSuite()
        {
            Properties properties = new Properties();
            properties.Set(Config.SECURITY_LEVEL, SECURITY_LEVEL.ToString());
            properties.Set(Config.HASH_ALGORITHM, HASH_ALGORITHM);
            return GetCryptoSuite(properties);
        }
    }
}