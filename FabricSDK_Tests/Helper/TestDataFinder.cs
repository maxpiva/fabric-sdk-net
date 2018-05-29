using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hyperledger.Fabric.Tests.Helper
{
    public static class TestDataFinder
    {
        public static string TestDataDirectory { get; set; }

        static TestDataFinder()
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
            return Path.Combine(TestDataDirectory, path);
        }
    }
}
