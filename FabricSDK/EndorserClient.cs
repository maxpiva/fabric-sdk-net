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
using Hyperledger.Fabric.Protos.Peer;
using Hyperledger.Fabric.Protos.Peer.FabricProposal;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;

namespace Hyperledger.Fabric.SDK
{
    /**
     * Sample client code that makes gRPC calls to the server.
     */
    public class EndorserClient
    {
        private static readonly ILog logger = LogProvider.GetLogger(typeof(EndorserClient));
        
        private Grpc.Core.Channel managedChannel;
        private Endorser.EndorserClient ecl;
        private bool shutdown = false;

        /**
         * Construct client for accessing Peer server using the existing channel.
         *
         * @param channelBuilder The ChannelBuilder to build the endorser client
         */
        public EndorserClient(Endpoint endpoint)
        {
            managedChannel = endpoint.BuildChannel();
            ecl=new Endorser.EndorserClient(managedChannel);
        }
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force) {
            if (shutdown) {
                return;
            }
            shutdown = true;
            Grpc.Core.Channel lchannel = managedChannel;
            // let all referenced resource finalize
            managedChannel = null;
            ecl = null;
            if (lchannel == null) {
                return;
            }
            if (force)
            {
                lchannel.ShutdownAsync().Wait();
            } else {
                bool isTerminated = false;

                try {
                    isTerminated = lchannel.ShutdownAsync().Wait(3*1000);
                } catch (Exception e) {
                    logger.DebugException(e.Message,e); //best effort
                }
                if (!isTerminated) {
                    lchannel.ShutdownAsync().Wait();
                }
            }
        }

        public async Task<Fabric.Protos.Peer.FabricProposalResponse.ProposalResponse> SendProposalAsync(SignedProposal proposal, CancellationToken token=default(CancellationToken)) 
        {
            if (shutdown)
            {
                throw new PeerException("Shutdown");
            }
            return await ecl.ProcessProposalAsync(proposal,null,null, token);
        }
        public Fabric.Protos.Peer.FabricProposalResponse.ProposalResponse SendProposal(SignedProposal proposal)
        {
            if (shutdown)
            {
                throw new PeerException("Shutdown");
            }
            return ecl.ProcessProposal(proposal);
        }

        public bool IsChannelActive()
        {
            Grpc.Core.Channel lchannel = managedChannel;
            return lchannel != null && lchannel.State!=ChannelState.Shutdown && lchannel.State!=ChannelState.TransientFailure;
        }

        ~EndorserClient()
        {
            Shutdown(true);

        }
    }
}

