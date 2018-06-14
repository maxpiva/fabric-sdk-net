using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Hyperledger.Fabric.SDK.Helper
{
    public class Properties : IDictionary<string, string>
    {
        private string filename;
        private Dictionary<string, string> list=new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);


        public void Add(string key, string value)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(string key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(string key, out string value)
        {
            throw new NotImplementedException();
        }

        public string this[string field]
        {
            get => Get(field);
            set => Set(field, value);
        }

        public ICollection<string> Keys => list.Keys;
        public ICollection<string> Values => list.Values;

        public string Get(string field, string defValue)
        {
            return Get(field) == null ? defValue : Get(field);
        }

        public string GetAndRemove(string field)
        {
            if (list.ContainsKey(field))
            {
                string value = list[field];
                list.Remove(field);
                return value;
            }

            return null;
        }

        public string Get(string field)
        {
            return list.ContainsKey(field) ? list[field] : null;
        }

        public bool Contains(string field)
        {
            return list.ContainsKey(field);
        }

        public void Set(string field, object value)
        {
            string val = null;
            if (value != null)
                val = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!list.ContainsKey(field))
                list.Add(field, val);
            else
                list[field] = val;
        }

        public void Save()
        {
            Save(filename);
        }

        public void Save(string filename)
        {
            this.filename = filename;

            StreamWriter file = new StreamWriter(filename);

            foreach (string prop in list.Keys.ToArray())
                if (!string.IsNullOrWhiteSpace(list[prop]))
                    file.WriteLine(prop + "=" + list[prop]);

            file.Close();
        }

        public void Reload()
        {
            if (!string.IsNullOrEmpty(filename))
                Load(filename);
        }

        public void Load(string filename)
        {
            this.filename = filename;
            list = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (File.Exists(filename))
                LoadFromFile(filename);
        }

        private void LoadFromFile(string file)
        {
            foreach (string line in File.ReadAllLines(file))
            {
                if (!string.IsNullOrEmpty(line) && !line.StartsWith(";") && !line.StartsWith("#") && !line.StartsWith("'") && line.Contains('='))
                {
                    int index = line.IndexOf('=');
                    string key = line.Substring(0, index).Trim();
                    string value = line.Substring(index + 1).Trim();

                    if (value.StartsWith("\"") && value.EndsWith("\"") || value.StartsWith("'") && value.EndsWith("'"))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }

                    list[key] = value;
                }
            }
        }

        public IEnumerator<KeyValuePair<string,string>> GetEnumerator()
        {
            foreach (KeyValuePair<string, string> str in list)
                yield return str;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Properties Clone()
        {
            Properties p=new Properties();
            p.filename = filename;
            p.list = list.ToDictionary(a => a.Key, a => a.Value);
            return p;
        }

        public void Add(KeyValuePair<string, string> item)
        {
            Set(item.Key,item.Value);
        }

        public void Clear()
        {
            list.Clear();
        }

        public bool Contains(KeyValuePair<string, string> item)
        {
            return list.Contains(item);
        }

        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, string> item)
        {
            return list.Remove(item.Key);
        }

        public int Count => list.Count;
        public bool IsReadOnly => false;
    }
}