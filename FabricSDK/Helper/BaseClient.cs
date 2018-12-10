using System;
using Newtonsoft.Json;

namespace Hyperledger.Fabric.SDK.Helper
{
    
    public class BaseClient
    {
        internal string id = Config.Instance.GetNextID();
        public Properties Properties { get; set; }=new Properties();

        public string Name { get; internal set; }
        
        public string Url { get; internal set; }
        
        public virtual Channel Channel { get; set; }

        internal bool shutdown = false;
        [JsonIgnore]
        public HFClient Client => Channel.Client;
        public BaseClient(string name, string url, Properties properties)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Invalid name");
            Exception e = Utils.CheckGrpcUrl(url);
            if (e != null)
                throw new ArgumentException("Bad url.", e);
            Url = url;
            Name = name;
            Properties = properties?.Clone();
        }

        public BaseClient()
        {
            
        }
    }
}
