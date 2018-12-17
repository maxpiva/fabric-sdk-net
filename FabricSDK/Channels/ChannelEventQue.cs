using System;
using System.Collections.Concurrent;
using Hyperledger.Fabric.SDK.Blocks;
using Hyperledger.Fabric.SDK.Exceptions;
using Hyperledger.Fabric.SDK.Logging;

namespace Hyperledger.Fabric.SDK.Channels
{
    public class ChannelEventQue
    {
        private static readonly ILog intlogger = LogProvider.GetLogger(typeof(ChannelEventQue));
        private readonly Channel channel;
        private readonly BlockingCollection<BlockEvent> events = new BlockingCollection<BlockEvent>(); //Thread safe
        private Exception eventException;

        public ChannelEventQue(Channel ch)
        {
            channel = ch;
        }

        public void EventError(Exception t)
        {
            eventException = t;
        }

        public bool AddBEvent(BlockEvent evnt)
        {
            if (channel.IsShutdown)
                return false;
            //For now just support blocks --- other types are also reported as blocks.

            if (!evnt.IsBlockEvent)
                return false;
            // May be fed by multiple eventhubs but BlockingQueue.add() is thread-safe
            events.Add(evnt);
            return true;
        }

        public BlockEvent GetNextEvent()
        {
            if (channel.IsShutdown)
                throw new EventHubException($"Channel {channel.Name} has been shutdown");
            BlockEvent ret = null;
            if (eventException != null)
                throw new EventHubException(eventException);
            try
            {
                ret = events.Take();
            }
            catch (Exception e)
            {
                if (channel.IsShutdown)
                    throw new EventHubException($"Channel {channel.Name} has been shutdown");
                intlogger.WarnException(e.Message, e);
                if (eventException != null)
                {
                    EventHubException eve = new EventHubException(e);
                    intlogger.ErrorException(eve.Message, eve);
                    throw eve;
                }
            }

            if (eventException != null)
                throw new EventHubException(eventException);
            if (channel.IsShutdown)
                throw new EventHubException($"Channel {channel.Name} has been shutdown.");
            return ret;
        }
    }
}