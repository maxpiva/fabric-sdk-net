/*
 *  Copyright 2016, 2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
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
package org.hyperledger.fabric.sdk;

import java.util.ArrayList;
import java.util.List;
import java.util.Properties;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import io.grpc.ConnectivityState;
import io.grpc.ManagedChannel;
import io.grpc.ManagedChannelBuilder;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import io.grpc.stub.StreamObserver;
import org.apache.commons.logging.Log;
import org.apache.commons.logging.LogFactory;
import org.hyperledger.fabric.protos.common.Common;
import org.hyperledger.fabric.protos.orderer.Ab;
import org.hyperledger.fabric.protos.orderer.Ab.DeliverResponse;
import org.hyperledger.fabric.protos.orderer.AtomicBroadcastGrpc;
import org.hyperledger.fabric.sdk.exception.TransactionException;
import org.hyperledger.fabric.sdk.helper.Config;

import static java.lang.String.format;
import static org.hyperledger.fabric.protos.orderer.Ab.DeliverResponse.TypeCase.STATUS;*/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Grpc.Core;
using Hyperledger.Fabric.Protos.Common;
using Hyperledger.Fabric.Protos.Orderer;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;
using Hyperledger.Fabric.SDK.NetExtensions;
using Config = Hyperledger.Fabric.SDK.Helper.Config;
using Status = Hyperledger.Fabric.Protos.Common.Status;

namespace Hyperledger.Fabric.SDK
{
    /**
     * Sample client code that makes gRPC calls to the server.
     */
    public class OrdererClient
    {
        private static readonly Config config = Config.GetConfig();
        private static readonly long ORDERER_WAIT_TIME = config.GetOrdererWaitTime();
        private static readonly ILog logger = LogProvider.GetLogger(typeof(OrdererClient));
        private readonly Endpoint endPoint;
        private readonly string channelName;
        private readonly string name;
        private readonly long ordererWaitTimeMilliSecs;
        private readonly string url;
        private Grpc.Core.Channel managedChannel = null;
        private bool shutdown = false;

        /**
         * Construct client for accessing Orderer server using the existing managedChannel.
         */
        public OrdererClient(Orderer orderer, Endpoint endPoint, Dictionary<string, object> properties)
        {
            this.endPoint = endPoint;
            name = orderer.Name;
            url = orderer.Url;
            channelName = orderer.Channel.Name;

            ordererWaitTimeMilliSecs = ORDERER_WAIT_TIME;

            if (properties != null && properties.ContainsKey("ordererWaitTimeMilliSecs"))
            {
                string ordererWaitTimeMilliSecsString = (string) properties["ordererWaitTimeMilliSecs"];
                if (!long.TryParse(ordererWaitTimeMilliSecsString, out ordererWaitTimeMilliSecs))
                {
                    logger.Warn($"Orderer {name} wait time {ordererWaitTimeMilliSecsString} not parsable.");
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Shutdown(bool force)
        {
            if (shutdown)
            {
                return;
            }

            shutdown = true;
            Grpc.Core.Channel lchannel = managedChannel;
            managedChannel = null;
            if (lchannel == null)
            {
                return;
            }

            if (force)
            {
                lchannel.ShutdownAsync().Wait();
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
                    lchannel.ShutdownAsync().Wait();
                }
            }
        }

        ~OrdererClient()
        {
            Shutdown(true);
        }

        public BroadcastResponse SendTransaction(Envelope envelope)
        {
            if (shutdown)
                throw new TransactionException("Orderer client is shutdown");
            Grpc.Core.Channel lmanagedChannel = managedChannel;

            if (lmanagedChannel == null || lmanagedChannel.State == ChannelState.TransientFailure || lmanagedChannel.State == ChannelState.Shutdown)
            {
                lmanagedChannel = endPoint.BuildChannel();
                managedChannel = lmanagedChannel;
                AtomicBroadcast.AtomicBroadcastClient nso;
                try
                {
                    CountDownLatch finishLatch = new CountDownLatch(1);
                    BroadcastResponse ret = null;
                    Exception throwable = null;

                    nso = new AtomicBroadcast.AtomicBroadcastClient(lmanagedChannel);
                    using (var call = nso.Broadcast())
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                while (await call.ResponseStream.MoveNext())
                                {
                                    BroadcastResponse resp = call.ResponseStream.Current;
                                    logger.Debug("resp status value: " + resp.Status + ", resp: " + resp.Info);
                                    if (resp.Status == Status.Success)
                                    {
                                        ret = resp;
                                    }
                                    else
                                    {
                                        throwable = new TransactionException($"Channel {channelName} orderer {name} status returned failure code {resp.Status}x ({resp.Info}) during order registration");
                                    }

                                    finishLatch.Signal();
                                }

                                Grpc.Core.Status stats = call.GetStatus();
                                if (stats.StatusCode != StatusCode.OK)
                                {
                                    if (!shutdown)
                                    {
                                        throwable = new TransactionException($"Channel {channelName} orderer {name} status finished with failure code {stats.StatusCode} ({stats.Detail}) during order registration");
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                if (!shutdown)
                                {
                                    logger.Error($"Received error on channel  {channelName} orderer {name}, url {url}, {e.Message}");
                                }

                                throwable = e;
                            }

                            finishLatch.Signal();
                        });
                        call.RequestStream.WriteAsync(envelope).Wait();
                        try
                        {
                            if (!finishLatch.Wait((int) ordererWaitTimeMilliSecs))
                            {
                                TransactionException ste = new TransactionException($"Channel {channelName}, send transactions failed on orderer {name}. Reason:  timeout after {ordererWaitTimeMilliSecs} ms.");
                                logger.ErrorException("sendTransaction error " + ste.Message, ste);
                                throw ste;
                            }

                            if (throwable != null)
                            {
                                Exception t = throwable;
                                if (t is RpcException)
                                {
                                    RpcException sre = (RpcException) t;
                                    logger.Error($"grpc status Code:{sre.StatusCode}, Description {sre.Status.Detail} {sre.Message}");
                                }

                                //get full stack trace
                                TransactionException ste = new TransactionException($"Channel {channelName}, send transaction failed on orderer {name}. Reason: {throwable.Message}", throwable);
                                logger.ErrorException("sendTransaction error " + ste.Message, ste);
                                throw ste;
                            }

                            logger.Debug("Done waiting for reply! Got:" + ret);
                        }
                        catch (Exception e)
                        {
                            logger.ErrorException(e.Message, e);
                        }

                        return ret;
                    }
                }
                catch (Exception t)
                {
                    managedChannel = null;
                    throw t;
                }
            }

            return null;
        }

        public DeliverResponse[] SendDeliver(Envelope envelope)
        {
            if (shutdown)
                throw new TransactionException("Orderer client is shutdown");
            Grpc.Core.Channel lmanagedChannel = managedChannel;

            if (lmanagedChannel == null || lmanagedChannel.State == ChannelState.TransientFailure || lmanagedChannel.State == ChannelState.Shutdown)
            {
                lmanagedChannel = endPoint.BuildChannel();
                managedChannel = lmanagedChannel;
                AtomicBroadcast.AtomicBroadcastClient nso;
                try
                {
                    CountDownLatch finishLatch = new CountDownLatch(1);
                    List<DeliverResponse> retList = new List<DeliverResponse>();
                    Exception throwable = null;

                    nso = new AtomicBroadcast.AtomicBroadcastClient(lmanagedChannel);
                    using (var call = nso.Deliver())
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                bool done = false;
                                while (await call.ResponseStream.MoveNext())
                                {
                                    // logger.info("Got Broadcast response: " + resp);
                                    DeliverResponse resp = call.ResponseStream.Current;
                                    logger.Debug("resp status value: " + resp.Status + ", type case: " + resp.TypeCase);
                                    if (done)
                                    {
                                        break;
                                    }

                                    if (resp.TypeCase == DeliverResponse.TypeOneofCase.Status)
                                    {
                                        done = true;
                                        retList.Insert(0, resp);
                                        finishLatch.Signal();
                                    }
                                    else
                                    {
                                        retList.Add(resp);
                                    }
                                }

                                Grpc.Core.Status stats = call.GetStatus();
                                if (stats.StatusCode != StatusCode.OK)
                                {
                                    if (!shutdown)
                                    {
                                        throwable = new TransactionException($"Channel {channelName} orderer {name} status finished with failure code {stats.StatusCode} ({stats.Detail}) during order registration");
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                if (!shutdown)
                                {
                                    logger.Error($"Received error on channel  {channelName} orderer {name}, url {url}, {e.Message}");
                                }

                                throwable = e;
                            }

                            finishLatch.Signal();
                        });
                        call.RequestStream.WriteAsync(envelope).Wait();
                        try
                        {
                            if (!finishLatch.Wait((int) ordererWaitTimeMilliSecs))
                            {
                                TransactionException ste = new TransactionException($"Channel {channelName}, sendDeliver failed on orderer {name}. Reason:  timeout after {ordererWaitTimeMilliSecs} ms.");
                                logger.ErrorException("sendDeliver error " + ste.Message, ste);
                                throw ste;
                            }

                            if (throwable != null)
                            {
                                Exception t = throwable;
                                if (t is RpcException)
                                {
                                    RpcException sre = (RpcException) t;
                                    logger.Error($"grpc status Code:{sre.StatusCode}, Description {sre.Status.Detail} {sre.Message}");
                                }

                                //get full stack trace
                                TransactionException ste = new TransactionException($"Channel {channelName}, sendDeliver  failed on orderer {name}. Reason: {throwable.Message}", throwable);
                                logger.ErrorException("sendDeliver error " + ste.Message, ste);
                                throw ste;
                            }

                            logger.Debug("Done waiting for reply!");
                        }
                        catch (Exception e)
                        {
                            logger.ErrorException(e.Message, e);
                        }

                        return retList.ToArray();
                    }
                }
                catch (Exception t)
                {
                    managedChannel = null;
                    throw t;
                }
            }

            return null;
        }

        public bool IsChannelActive()
        {
            Grpc.Core.Channel lchannel = managedChannel;
            return lchannel != null && lchannel.State != ChannelState.Shutdown && lchannel.State != ChannelState.TransientFailure;
        }
    }
}