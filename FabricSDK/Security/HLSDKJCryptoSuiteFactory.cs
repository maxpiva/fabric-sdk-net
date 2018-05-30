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
 /*
package org.hyperledger.fabric.sdk.security;

import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.util.Map;
import java.util.Properties;
import java.util.concurrent.ConcurrentHashMap;

import org.hyperledger.fabric.sdk.exception.CryptoException;
import org.hyperledger.fabric.sdk.exception.InvalidIllegalArgumentException;
import org.hyperledger.fabric.sdk.helper.Config;
*/
/**
 * SDK's Default implementation of CryptoSuiteFactory.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Security
{


    public class HLSDKJCryptoSuiteFactory : ICryptoSuiteFactory
    {
        private readonly int SECURITY_LEVEL = Config.Instance.GetSecurityLevel();
        private readonly String HASH_ALGORITHM = Config.Instance.GetHashAlgorithm();

        internal HLSDKJCryptoSuiteFactory()
        {

        }

        private static readonly ConcurrentDictionary<Properties, ICryptoSuite> cache = new ConcurrentDictionary<Properties, ICryptoSuite>();


        public ICryptoSuite GetCryptoSuite(Properties properties)
        {
            ICryptoSuite ret = null;
            foreach (Properties st in cache.Keys)
            {
                bool found = true;
                foreach (string key in properties)
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
                cache[properties]= ret;
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

    

        /*
        if (null == theFACTORY) {

            String cf = config.getDefaultCryptoSuiteFactory();
            if (null == cf || cf.isEmpty() || cf.equals(Security.HLSDKJCryptoSuiteFactory.class.getName())) { // Use this class as the factory.

                theFACTORY = Security.Factory.Instance.);

            } else {

                // Invoke static method instance on factory class specified by config properties.
                // In this case this class will no longer be used as the factory.

                Class<?> aClass = Class.forName(cf);

                Method method = aClass.getMethod("instance");
                Object theFACTORYObject = method.invoke(null);
                if (null == theFACTORYObject) {
                    throw new InstantiationException(String.format("Class specified by %s has instance method returning null.  Expected object implementing CryptoSuiteFactory interface.", cf));
                }

                if (!(theFACTORYObject instanceof CryptoSuiteFactory)) {

                    throw new InstantiationException(String.format("Class specified by %s has instance method returning a class %s which does not implement interface CryptoSuiteFactory ",
                            cf, theFACTORYObject.getClass().getName()));

                }

                theFACTORY = (CryptoSuiteFactory) theFACTORYObject;

            }
        }

        return theFACTORY;
    }
    */
    }
}