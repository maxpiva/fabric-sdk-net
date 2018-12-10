using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hyperledger.Fabric.Tests.Helper
{
    public static class Utils
    {
        public static string TestDataDirectory { get; set; }

        static Utils()
        {
            string CurrentDir = Directory.GetCurrentDirectory();
            while (CurrentDir != null)
            {
                if (Directory.Exists(Path.Combine(CurrentDir,"TestData")) && Directory.Exists(Path.Combine(CurrentDir,"TestData","Fixture")) &&
                    Directory.Exists(Path.Combine(CurrentDir,"TestData","Resources")))
                {
                    TestDataDirectory = Path.Combine(CurrentDir,"TestData");
                    return;
                }
                CurrentDir=Directory.GetParent(CurrentDir)?.FullName;
            }
        }

        public static string Locate(this string path)
        {
            return Path.Combine(TestDataDirectory, path.Replace("//","/").Replace("/",Path.DirectorySeparatorChar.ToString()).Replace("\\",Path.DirectorySeparatorChar.ToString()));
        }
        public static int IndexOf(this byte[] data, byte[] search)
        {
            int pos = 0;
            int fcount = 0;
            while (pos < data.Length)
            {
                if (data[pos] == search[fcount])
                    fcount++;
                else
                    fcount = 0;
                if (fcount == search.Length)
                    return pos - fcount + 1;
                pos++;
            }
            return -1;
        }

        public static bool Contains(this byte[] data, byte[] search)
        {
            return IndexOf(data, search) != -1;
        }
    }
}
