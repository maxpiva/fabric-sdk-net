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
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Discovery
{
    public static class ISDAdditionInfoExtensions
    {
        public static byte[] GetAllTLSCerts(this ISDAdditionInfo info)
        {
            try
            {
                using (System.IO.MemoryStream outputStream = new System.IO.MemoryStream())
                {
                    if (info.TLSCerts != null)
                    {
                        foreach (byte[] tlsCert in info.TLSCerts)
                            outputStream.Write(tlsCert, 0, tlsCert.Length);
                    }
                    if (info.TLSIntermediateCerts != null)
                    {
                        foreach (byte[] tlsCert in info.TLSIntermediateCerts)
                            outputStream.Write(tlsCert, 0, tlsCert.Length);
                    }
                    return outputStream.ToArray();
                }
            }
            catch (Exception e)
            {
                throw new ServiceDiscoveryException(e.Message,e);
            }
        }

        public static string FindClientProp(this ISDAdditionInfo info, Properties config, string prop, string def)
        {
            string[] split = info.Endpoint.Split(':');
            string endpointHost = split[0];
            string ret = config.Get("org.hyperledger.fabric.sdk.discovery.default." + prop, def);
            ret = config.Get("org.hyperledger.fabric.sdk.discovery.mspid." + prop + "." + info.MspId, ret);
            ret = config.Get("org.hyperledger.fabric.sdk.discovery.endpoint." + prop + "." + endpointHost, ret);
            ret = config.Get("org.hyperledger.fabric.sdk.discovery.endpoint." + prop + "." + info.Endpoint, ret);
            return ret;

        }

    }
}