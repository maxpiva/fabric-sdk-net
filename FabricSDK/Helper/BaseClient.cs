using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Hyperledger.Fabric.SDK.Exceptions;

namespace Hyperledger.Fabric.SDK.Helper
{
    [DataContract]
    public class BaseClient
    {
        [DataMember]
        public Properties Properties { get; internal set; }
        [DataMember]
        public string Name { get; internal set; }
        [DataMember]
        public string Url { get; internal set; }
        [DataMember]
        public virtual Channel Channel { get; set; }

        internal bool shutdown = false;

        public BaseClient(string name, string url, Properties properties)
        {
            if (string.IsNullOrEmpty(name))
                throw new InvalidArgumentException("Invalid name");
            Exception e = Utils.CheckGrpcUrl(url);
            if (e != null)
                throw new InvalidArgumentException("Bad url.", e);
            Url = url;
            Name = name;
            Properties = properties?.Clone();
        }

        public BaseClient()
        {

        }
    }
}
