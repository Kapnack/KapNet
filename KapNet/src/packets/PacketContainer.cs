using System.Collections.Generic;

namespace Network.BaseDLL
{
    public class PacketContainer<TKey1, TKey2, TKey3, TValue>
    {
        private Dictionary<TKey1, Dictionary<TKey2, Dictionary<TKey3, TValue>>> container = new Dictionary<TKey1, Dictionary<TKey2, Dictionary<TKey3, TValue>>>();

        public void Add(TKey1 key1, TKey2 key2, TKey3 key3, TValue value)
        {
            if (!container.TryGetValue(key1, out Dictionary<TKey2, Dictionary<TKey3, TValue>> diccionary2))
            {
                diccionary2 = new Dictionary<TKey2, Dictionary<TKey3, TValue>>();
                container.Add(key1, diccionary2);
            }

            if (!diccionary2.TryGetValue(key2, out Dictionary<TKey3, TValue> diccionary3))
            {
                diccionary3 = new Dictionary<TKey3, TValue>();
                container[key1].Add(key2, diccionary3);
            }

            if (diccionary3.ContainsKey(key3))
                container[key1][key2][key3] = value;
            else
                container[key1][key2].Add(key3, value);

        }

        public bool Contains(TKey1 key1, TKey2 key2, TKey3 key3)
        {
            if (!container.TryGetValue(key1, out Dictionary<TKey2, Dictionary<TKey3, TValue>> diccionary2))
                return false;

            if (!diccionary2.TryGetValue(key2, out Dictionary<TKey3, TValue> diccionary3))
                return false;

            return diccionary3.ContainsKey(key3);
        }


        public bool ContainsKey1(TKey1 key1)
        {
            return container.ContainsKey(key1);
        }

        public bool ContainsKey2(TKey1 key1, TKey2 key2)
        {
            if (!container.ContainsKey(key1))
                return false;

            return container[key1].ContainsKey(key2);
        }

        public TValue Get(TKey1 key1, TKey2 key2, TKey3 key3)
        {
            if (Contains(key1, key2, key3))
                return container[key1][key2][key3];

            return default;
        }

        public void Remove(TKey1 key1, TKey2 key2, TKey3 key3)
        {
            if (!Contains(key1, key2, key3))
                return;

            container[key1][key2].Remove(key3);
        }
    }
}
