using System.Threading;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class CountDownLatch
    {
        private int remain;
        private EventWaitHandle e;

        public CountDownLatch(int count)
        {
            remain = count;
            e = new ManualResetEvent(false);
        }

        public void Signal()
        {
            if (Interlocked.Decrement(ref remain) == 0)
                e.Set();
        }

        public bool Wait()
        {
            return e.WaitOne();
        }
        public bool Wait(int mill)
        {
            return e.WaitOne(mill);
        }
    }
}