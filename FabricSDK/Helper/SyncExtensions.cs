using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hyperledger.Fabric.SDK.Helper
{
    public static class SyncExtensions
    {
        public static T RunAndUnwarp<T>(this Task<T> func)
        {
            try
            {
                return func.GetAwaiter().GetResult();
            }
            catch (AggregateException e)
            {
                throw e.Flatten().InnerExceptions.First();
            }
        }
        public static void RunAndUnwarp(this Task func)
        {
            try
            {
                func.GetAwaiter().GetResult();
            }
            catch (AggregateException e)
            {
                throw e.Flatten().InnerExceptions.First();
            }
        }
    }
}
