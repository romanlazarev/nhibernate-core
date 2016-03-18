// 2015-12-22 - Re-implemented by Ugasoft, LLC to remove Apache copyrighted code that may conflict with GPL licensing

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHibernate.Util
{
    /// <summary>
    /// LRUMap is a type of <see cref="SequencedHashMap"/> which has a maxium size and uses Least Recent Used algorithm 
    /// to remove items from the map when the maxiumum size is reached and new items are added. 
    /// This class should be derived from SequencedHashMap and should support all the operations of SequencedHashMap. 
    /// User should be able to specify the maximum number of items to be stored before the least recently used items get removed from the collection.
    /// </summary>
    [Serializable]
    public class LRUMap : SequencedHashMap
    {
        private int maximumSize;

        public LRUMap()
            : this(100) { }

        public LRUMap(int capacity)
            : base(capacity)
        {
            maximumSize = capacity;
        }

        public override object this[object key]
        {
            get
            {
                var obj = base[key];
                if(obj != null)
                {
                    Remove(key);
                    base.Add(key, obj);
                    return obj;
                }
                return null;
            }
            set
            {
                int mapSize = Count;
                if (mapSize >= maximumSize)
                {
                    if (!ContainsKey(key))
                    {
                        Remove(FirstKey);
                    }
                }

                base[key] = value;
            }
        }

        public int MaximumSize
        {
            get { return maximumSize; }
            set
			{
				maximumSize = value;
                while (Count > maximumSize)
                {
                    Remove(FirstKey);
                }
			}
        }

    }
}
