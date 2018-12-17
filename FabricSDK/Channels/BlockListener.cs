using System;
using Hyperledger.Fabric.SDK.Blocks;
using Hyperledger.Fabric.SDK.Helper;
using Hyperledger.Fabric.SDK.Logging;

namespace Hyperledger.Fabric.SDK.Channels
{
    public class BlockListener
    {
        public static readonly string BLOCK_LISTENER_TAG = "BLOCK_LISTENER_HANDLE";
        private static readonly ILog intlogger = LogProvider.GetLogger(typeof(BlockListener));

        public BlockListener(Channel ch, Action<BlockEvent> listenerAction)
        {
            Channel = ch;
            Handle = BLOCK_LISTENER_TAG + Utils.GenerateUUID() + BLOCK_LISTENER_TAG;
            intlogger.Debug($"Channel {Channel.Name} blockListener {Handle} starting");
            ListenerAction = listenerAction;
            lock (Channel.blockListeners)
            {
                Channel.blockListeners.Add(Handle, this);
            }
        }

        public Channel Channel { get; }

        public Action<BlockEvent> ListenerAction { get; }
        public string Handle { get; }
    }
}