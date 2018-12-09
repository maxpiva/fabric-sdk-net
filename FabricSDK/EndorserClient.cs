/*
 *  Copyright 2016 DTCC, Fujitsu Australia Software Technology - All Rights Reserved.
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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Discovery;
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.Protos.Peer.FabricProposalResponse;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Response = Hyperledger.Fabric.Protos.Discovery.Response;

namespace Hyperledger.Fabric.SDK
{
    /**
     * Sample client code that makes gRPC calls to the server.
     */
    public class EndorserClient
    {

        private static readonly ILog logger = LogProvider.GetLogger(typeof(EndorserClient));
        private static readonly bool IS_TRACE_LEVEL = logger.IsTraceEnabled();

        private Protos.Discovery.Discovery.DiscoveryClient dfs;
        private Endorser.EndorserClient ecl;

        private Grpc.Core.Channel managedChannel;
        private bool shutdown;
        private string toString;


        /**
         * Construct client for accessing Peer server using the existing channel.
         *
         * @param channelBuilder The ChannelBuilder to build the endorser client
         */
        public EndorserClient(string channelName, string name, Endpoint endpoint)
        {
            managedChannel = endpoint.BuildChannel();
            ecl = new Endorser.EndorserClient(managedChannel);
            dfs = new Protos.Discovery.Discovery.DiscoveryClient(managedChannel);
            toString = $"EndorserClient{{id: {Config.Instance.GetNextID()}, channel: {channelName}, name:{name}, url: {endpoint.Url}}}";
            logger.Trace("Created " + ToString());
        }

        public virtual bool IsChannelActive
        {
            get
            {
                Grpc.Core.Channel lchannel = managedChannel;
                if (null == lchannel)
                {
                    logger.Trace($"{ToString()} Grpc channel needs creation.");
                    return false;
                }
                bool isTransientFailure = lchannel.State == ChannelState.TransientFailure;
                bool isShutdown = lchannel.State == ChannelState.Shutdown;
                bool ret = !isShutdown && !isTransientFailure;
                if (IS_TRACE_LEVEL)
                    logger.Trace("%s grpc channel isActive: %b, isShutdown: %b, isTransientFailure: %b, state: %s ");
                return ret;
            }
        }

        public override string ToString()
        {
            return toString;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (IS_TRACE_LEVEL)
                logger.Trace("%s shutdown called force: %b, shutdown: %b, managedChannel: %s");
            if (shutdown)
                return;

            shutdown = true;
            Grpc.Core.Channel lchannel = managedChannel;
            // let all referenced resource finalize
            managedChannel = null;
            ecl = null;
            dfs = null;
            if (lchannel == null)
            {
                return;
            }

            if (force)
            {
                try
                {
                    lchannel.ShutdownAsync().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    logger.WarnException(e.Message, e);
                }
            }
            else
            {
                bool isTerminated = false;

                try
                {
                    isTerminated = lchannel.ShutdownAsync().Wait(3 * 1000);
                }
                catch (Exception e)
                {
                    logger.DebugException(e.Message, e); //best effort
                }

                if (!isTerminated)
                {
                    try
                    {
                        lchannel.ShutdownAsync().GetAwaiter().GetResult();
                    }
                    catch (Exception e)
                    {
                        logger.WarnException(e.Message, e);
                    }
                }
            }
        }

        public virtual async Task<ProposalResponse> SendProposalAsync(SignedProposal proposal, CancellationToken token = default(CancellationToken))
        {
            if (shutdown)
            {
                throw new PeerException("Shutdown");
            }

            return await ecl.ProcessProposalAsync(proposal, null, null, token);
        }

        public virtual Task<Response> SendDiscoveryRequestAsync(SignedRequest signedRequest, int? milliseconds = null, CancellationToken token = default(CancellationToken))
        {
            if (shutdown)
                throw new PeerException("Shutdown");
            if (milliseconds.HasValue && milliseconds > 0)
                return SendDiscoveryRequestInternalAsync(signedRequest, token).TimeoutAsync(TimeSpan.FromMilliseconds(milliseconds.Value),token);
            return SendDiscoveryRequestInternalAsync(signedRequest, token);
        }

        public Response SendDiscoveryRequest(SignedRequest signedRequest, int? milliseconds = null)
        {
            return SendDiscoveryRequestAsync(signedRequest, milliseconds).RunAndUnwarp();
        }

        private async Task<Response> SendDiscoveryRequestInternalAsync(SignedRequest signedRequest, CancellationToken token)
        {
            return await dfs.DiscoverAsync(signedRequest, null, null, token);
        }

        public ProposalResponse SendProposal(SignedProposal proposal)
        {
            if (shutdown)
                throw new PeerException("Shutdown");
            return ecl.ProcessProposal(proposal);
        }

        ~EndorserClient()
        {
            if (!shutdown)
            {
                logger.Warn($"{ToString()} finalized not shutdown is Active {IsChannelActive}");
            }

            Shutdown(true);
        }
    }
}