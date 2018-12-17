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
using System.Threading;
using System.Threading.Tasks;
using Hyperledger.Fabric.SDK.Channels;
using Hyperledger.Fabric.SDK.Helper;

namespace Hyperledger.Fabric.SDK.Discovery
{
    public interface ISDAdditionInfo
    {
        Channel Channel { get; }
        HFClient Client { get; }
        string Endpoint { get; }
        string MspId { get; }
        List<byte[]> TLSCerts { get; }
        List<byte[]> TLSIntermediateCerts { get; }
    }
    public interface ISDAdditionInfo<T> : ISDAdditionInfo where T: BaseClient
    {
        IReadOnlyDictionary<string, T> EndpointMap { get; }
        Task<T> AddAsync(Properties config, CancellationToken token);
    }
}
