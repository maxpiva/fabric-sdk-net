/*
 *
 *  Copyright 2016,2017 DTCC, Fujitsu Australia Software Technology, IBM - All Rights Reserved.
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *     http://www.apache.org/licenses/LICENSE-2.0
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Hyperledger.Fabric.SDK.Helper
{
    /**
     * Dumps files for diagnostic purposes
     */
    public class DiagnosticFileDumper
    {
        //  private static final Log logger = LogFactory.getLog(DiagnosticFileDumper.class);
        private static Thread thread;

        private static DiagnosticFileDumper singleInstance;
        private static int counter;
        private readonly string dirAbsolutePath;

        private readonly string pid;
        private readonly BlockingCollection<QueEntry> queEntries = new BlockingCollection<QueEntry>();
        private readonly DirectoryInfo directory;

        private DiagnosticFileDumper(DirectoryInfo directory)
        {
            this.directory = directory;
            dirAbsolutePath = directory?.FullName;
            pid = getPID() + "";
        }

        public static DiagnosticFileDumper ConfigInstance(DirectoryInfo directory)
        {
            if (singleInstance == null)
            {
                singleInstance = new DiagnosticFileDumper(directory);
                thread = new Thread(() => singleInstance.Run(null));
                thread.Start();
            }

            return singleInstance;
        }

        public string CreateDiagnosticProtobufFile(byte[] byteString)
        {
            return CreateDiagnosticFile(byteString, "protobuf_", "proto");
        }

        private bool CantWrite()
        {
            return null == directory || !directory.Exists || (directory.Attributes & FileAttributes.ReadOnly) != 0;
        }

        public string CreateDiagnosticFile(byte[] bytes)
        {
            return CreateDiagnosticFile(bytes, null, null);
        }

        public string CreateDiagnosticTarFile(byte[] bytes)
        {
            return CreateDiagnosticFile(bytes, null, "tgz");
        }

        public string CreateDiagnosticFile(string msg)
        {
            return CreateDiagnosticFile(Encoding.UTF8.GetBytes(msg), null, null);
        }

        public string CreateDiagnosticFile(byte[] bytes, string prefix, string ext)
        {
            string fileName = "";
            if (CantWrite())
            {
                return "Missing dump directory or can not write: " + dirAbsolutePath;
            }

            if (null != bytes)
            {
                if (null == prefix)
                {
                    prefix = "diagnostic_";
                }

                if (null == ext)
                {
                    ext = "bin";
                }

                fileName = prefix + DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss_SSS") + "P" + pid + "_" + Thread.CurrentThread.Name + "_" + counter + "." + ext;
                counter++;
                fileName = fileName.Replace("\\:", "-"); // colon is bad for windows.
                queEntries.Add(new QueEntry(fileName, bytes)); //Add to Que let process by async thread.
            }

            return fileName;
        }


        public void Run(object o)
        {
            while (true)
            {
                try
                {
                    LinkedList<QueEntry> entries = new LinkedList<QueEntry>();
                    QueEntry entry;
                    if (queEntries.TryTake(out entry))
                        entries.AddFirst(entry);
                    while (queEntries.TryTake(out entry))
                    {
                        entries.AddAfter(entries.Last, entry);
                    }

                    if (CantWrite())
                    {
                        return; //IF the directory is missing just assume user does not want diagnostic files created anymore.
                    }

                    foreach (QueEntry q in entries)
                    {
                        try
                        {
                            string finalpath = Path.Combine(dirAbsolutePath, q.FileName);
                            File.WriteAllBytes(finalpath, q.DataBytes);
                        }
                        catch (Exception)
                        {
                            //best effort
                        }
                    }
                }
                catch (Exception)
                {
                    // best effort
                }
            }
        }

        private static string getPID()
        {
            return Thread.CurrentThread.Name;
        }

        public class QueEntry
        {
            public byte[] DataBytes;
            public string FileName;

            public QueEntry(string fileName, byte[] dataBytes)
            {
                FileName = fileName;
                DataBytes = dataBytes;
            }
        }
    }
}