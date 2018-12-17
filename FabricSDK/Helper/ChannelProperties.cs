using System.Collections.Generic;
using Grpc.Core;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class ChannelProperties
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public ChannelCredentials Credentials { get; set; }
        public List<ChannelOption> Options { get; set; } = new List<ChannelOption>();

        public Channel CreateChannel() => new Channel(Host, Port, Credentials, Options);
    }
}