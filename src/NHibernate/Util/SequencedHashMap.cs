// 2015-12-22 - Re-implemented by Ugasoft, LLC to remove Apache copyrighted code that may conflict with GPL licensing

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace NHibernate.Util
{
    [Serializable]
    public class SequencedHashMap : IDictionary, IDeserializationCallback
    {
        /// <summary>
        /// Circular Doubly Linked List
        /// </summary>
        [Serializable]
        private class SequenceRecord
        {
            private object _key;
            private object _value;

            private SequenceRecord _nextRecord = null;
            private SequenceRecord _prevRecord = null;

            public SequenceRecord()
                : this(null, null)
            {
            }

            public SequenceRecord(object key, object value)
            {
                _key = key;
                _value = value;
            }

            public object Key
            {
                get { return _key; }
            }

            public object Value
            {
                get { return _value; }
                set { _value = value; }
            }

            public SequenceRecord NextRecord
            {
                get { return _nextRecord; }
                set { _nextRecord = value; }
            }

            public SequenceRecord PrevRecord
            {
                get { return _prevRecord; }
                set { _prevRecord = value; }
            }

            #region System.Object Members

            public override int GetHashCode()
            {
                return ((_key == null ? 0 : _key.GetHashCode()) ^ (_value == null ? 0 : _value.GetHashCode()));
            }

            public override bool Equals(object obj)
            {
                var other = obj as SequenceRecord;
                if (other == null) return false;
                if (other == this) return true;

                return ((_key == null ? other.Key == null : _key.Equals(other.Key)) &&
                        (_value == null ? other.Value == null : _value.Equals(other.Value)));
            }

            public override string ToString()
            {
                return string.Format("[{0}={1}]", _key, _value);
            }

            #endregion
        }

        [Serializable]
        protected abstract class KeyValueCollectionBase : ICollection
        {
            protected SequencedHashMap Container
            {
                get;
                private set;
            }

            #region Protected Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="KeyValueCollectionBase"/> class.
            /// </summary>
            /// <param name="container">The container.</param>
            protected KeyValueCollectionBase(SequencedHashMap container)
            {
                Container = container;
            }

            /// <summary>
            /// Gets the number of elements contained in the <see cref="T:System.Collections.ICollection" />.
            /// </summary>
            public int Count
            {
                get { return Container.Count; }
            }

            /// <summary>
            /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).
            /// </summary>
            public bool IsSynchronized
            {
                get { return Container.IsSynchronized; }
            }

            /// <summary>
            /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.
            /// </summary>
            public object SyncRoot
            {
                get { return Container.SyncRoot; }
            }

            #endregion Public Properties

            #region Public Methods

            /// <summary>
            /// Copies the elements of the <see cref="T:System.Collections.ICollection" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
            /// </summary>
            /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
            /// <param name="index">The zero-based index in <paramref name="array" /> at which copying begins.</param>
            public void CopyTo(Array array, int index)
            {
                var it = GetEnumerator();

                while (it.MoveNext())
                {
                    array.SetValue(it, index++);
                }
            }

            /// <summary>
            /// Returns an enumerator that iterates through a collection.
            /// </summary>
            /// <returns>
            /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
            /// </returns>
            public abstract IEnumerator GetEnumerator();

            #endregion Public Methods
        }

        private class KeyCollection : KeyValueCollectionBase
        {
            public KeyCollection(SequencedHashMap parent)
                : base(parent)
            {
            }

            public override IEnumerator GetEnumerator()
            {
                return new OrderedKeyEnumerator(Container);
            }
        }

        private class ValueCollection : KeyValueCollectionBase
        {
            public ValueCollection(SequencedHashMap parent)
                : base(parent)
            {
            }

            public override IEnumerator GetEnumerator()
            {
                return new OrderedValueEnumerator(Container);
            }
        }

        /// <summary>
        /// Abstract base class to enumerate Key, Value, and KeyValue (DictionaryEntry) values in CircularLinkedList 
        /// </summary>
        protected abstract class OrderedDictionaryEnumeratorBase : IDictionaryEnumerator
        {
            private SequencedHashMap _container;
            private SequenceRecord _current;
            private long _targetRevision;

            #region Protected Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="OrderedDictionaryEnumeratorBase"/> class.
            /// </summary>
            /// <param name="container">The container.</param>
            protected OrderedDictionaryEnumeratorBase(SequencedHashMap container)
            {
                _container = container;
                _current = container._scope;
                _targetRevision = _container._revision;
            }

            #endregion Protected Constructors

            #region Public Properties

            /// <summary>
            /// Gets the current object.
            /// </summary>
            public abstract object Current { get; }

            /// <summary>
            /// Gets both the key and the value of the current dictionary entry.
            /// </summary>
            public DictionaryEntry Entry
            {
                get { return new DictionaryEntry(_current.Key, _current.Value); }
            }

            /// <summary>
            /// Gets the key of the current dictionary entry.
            /// </summary>
            public object Key
            {
                get { return _current.Key; }
            }

            /// <summary>
            /// Gets the value of the current dictionary entry.
            /// </summary>
            public object Value
            {
                get { return _current.Value; }
            }

            #endregion Public Properties

            #region Public Methods

            /// <summary>
            /// Advances the enumerator to the next element of the collection.
            /// </summary>
            /// <returns>
            /// true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.
            /// </returns>
            /// <exception cref="System.InvalidOperationException">Enumerator was modified</exception>
            public bool MoveNext()
            {
                // Check the parent modCount and throw exception if collection is modified while enumerating
                if (_container._revision != _targetRevision)
                {
                    throw new InvalidOperationException("Enumerator was changed");
                }

                // traverse _objectList and return true/false indicating whether there are more items
                if (_current.NextRecord == _container._scope)
                {
                    return false;
                }
                _current = _current.NextRecord;

                return true;
            }

            /// <summary>
            /// Sets the enumerator to its initial position, which is before the first element in the collection.
            /// </summary>
            public void Reset()
            {
                _current = _container._scope;
            }

            #endregion Public Methods
        }

        private class OrderedEntryEnumerator : OrderedDictionaryEnumeratorBase
        {
            public override object Current
            {
                get { return Entry; }
            }

            public OrderedEntryEnumerator(SequencedHashMap container)
                : base(container)
            {
            }
        }

        private class OrderedKeyEnumerator : OrderedDictionaryEnumeratorBase
        {
            public override object Current
            {
                get { return Key; }
            }

            public OrderedKeyEnumerator(SequencedHashMap container)
                : base(container)
            {
            }
        }

        private class OrderedValueEnumerator : OrderedDictionaryEnumeratorBase
        {
            public override object Current
            {
                get { return Value; }
            }

            public OrderedValueEnumerator(SequencedHashMap container)
                : base(container)
            {
            }
        }

        #region Private Fields

        private object _syncRoot = new object();
        private readonly Hashtable _hashtableRecords;
        private SequenceRecord _scope;

        private long _revision; // sum of inserts, updates and deletes

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SequencedHashMap"/> class.
        /// </summary>
        public SequencedHashMap()
            : this(0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SequencedHashMap"/> class.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        public SequencedHashMap(int capacity)
            : this(capacity, 1.0F)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SequencedHashMap"/> class.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        /// <param name="loadFactor">The load factor.</param>
        public SequencedHashMap(int capacity, float loadFactor)
            : this(capacity, loadFactor, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SequencedHashMap"/> class.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        /// <param name="equalityComparer">The equality comparer.</param>
        public SequencedHashMap(int capacity, IEqualityComparer equalityComparer)
            : this(capacity, 1.0F, equalityComparer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SequencedHashMap"/> class.
        /// </summary>
        /// <param name="equalityComparer">The equality comparer.</param>
        public SequencedHashMap(IEqualityComparer equalityComparer)
            : this(0, 1.0F, equalityComparer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SequencedHashMap"/> class.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        /// <param name="loadFactor">The load factor.</param>
        /// <param name="equalityComparer">The equality comparer.</param>
        public SequencedHashMap(int capacity, float loadFactor, IEqualityComparer equalityComparer)
        {
            _scope = new SequenceRecord();
            _scope.PrevRecord = _scope;
            _scope.NextRecord = _scope;

            _hashtableRecords = new Hashtable(capacity, loadFactor, equalityComparer);
        }

        #endregion Public Constructors

        #region IDictionary implementation

        /// <summary>
        /// Adds an element with the provided key and value to the <see cref="T:System.Collections.IDictionary" /> object.
        /// </summary>
        /// <param name="key">The <see cref="T:System.Object" /> to use as the key of the element to add.</param>
        /// <param name="value">The <see cref="T:System.Object" /> to use as the value of the element to add.</param>
        public virtual void Add(object key, object value)
        {
            this[key] = value;
        }

        /// <summary>
        /// Removes all elements from the <see cref="T:System.Collections.IDictionary" /> object.
        /// </summary>
        public void Clear()
        {
            _revision++;
            _hashtableRecords.Clear();

            _scope.PrevRecord = _scope;
            _scope.NextRecord = _scope;
        }

        /// <summary>
        /// Determines whether the <see cref="T:System.Collections.IDictionary" /> object contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the <see cref="T:System.Collections.IDictionary" /> object.</param>
        /// <returns>
        /// true if the <see cref="T:System.Collections.IDictionary" /> contains an element with the key; otherwise, false.
        /// </returns>
        public bool Contains(object key)
        {
            return ContainsKey(key);
        }

        /// <summary>
        /// Returns an <see cref="T:System.Collections.IDictionaryEnumerator" /> object for the <see cref="T:System.Collections.IDictionary" /> object.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IDictionaryEnumerator" /> object for the <see cref="T:System.Collections.IDictionary" /> object.
        /// </returns>
        public IDictionaryEnumerator GetEnumerator()
        {
            return new OrderedEntryEnumerator(this);
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IDictionary" /> object has a fixed size.
        /// </summary>
        public bool IsFixedSize
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.IDictionary" /> object is read-only.
        /// </summary>
        public bool IsReadOnly
        {
            get { return false; }
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.ICollection" /> object containing the keys of the <see cref="T:System.Collections.IDictionary" /> object.
        /// </summary>
        public ICollection Keys
        {
            get { return new KeyCollection(this); }
        }

        /// <summary>
        /// Removes the element with the specified key from the <see cref="T:System.Collections.IDictionary" /> object.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        public void Remove(object key)
        {
            var record = _hashtableRecords[key] as SequenceRecord;
            if (record != null)
            {
                _hashtableRecords.Remove(key);
                _revision++;
                RemoveRecord(record);
            }
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("[");
            var current = _scope.NextRecord;
            while(current != _scope)
            {
                sb.Append(current.Key);
                sb.Append("=");
                sb.Append(current.Value);
                if(current.NextRecord != _scope)
                {
                    sb.Append(",");
                }
                current = current.NextRecord;
            }
            sb.Append("]");

            return sb.ToString();
        }

        /// <summary>
        /// Gets an <see cref="T:System.Collections.ICollection" /> object containing the values 
        /// in the <see cref="T:System.Collections.IDictionary" /> object.
        /// </summary>
        public ICollection Values
        {
            get { return new ValueCollection(this); }
        }

        /// <summary>
        /// Gets or sets the element with the specified key.
        /// </summary>
        /// <param name="objectKey">The key.</param>
        /// <returns></returns>
        public virtual object this[object objectKey]
        {
            get
            {
                var record = _hashtableRecords[objectKey] as SequenceRecord;
                if (record != null)
                    return record.Value;
                else return null;
            }
            set
            {
                _revision++;

                var record = _hashtableRecords[objectKey] as SequenceRecord;
                if (record != null)
                {
                    RemoveRecord(record);
                    record.Value = value;
                }
                else
                {
                    record = new SequenceRecord(objectKey, value);
                    _hashtableRecords[objectKey] = record;
                }

                InsertRecord(record);
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        public void CopyTo(Array array, int index)
        {
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
            {
                array.SetValue(enumerator.Current, index++);
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.ICollection" />.
        /// </summary>
        public int Count
        {
            get { return _hashtableRecords.Count; }
        }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).
        /// </summary>
        public bool IsSynchronized
        {
            get { return false; }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.
        /// </summary>
        public object SyncRoot
        {
            get { return _syncRoot; }
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new OrderedEntryEnumerator(this);
        }

        #endregion

        #region IDeserializationCallback implementation

        /// <summary>
        /// Runs when the entire object graph has been deserialized.
        /// </summary>
        /// <param name="sender">The object that initiated the callback. The functionality for this parameter is not currently implemented.</param>
        public void OnDeserialization(object sender)
        {
            _hashtableRecords.OnDeserialization(sender);
        }

        #endregion

        private void RemoveRecord(SequenceRecord record)
        {
            record.NextRecord.PrevRecord = record.PrevRecord;
            record.PrevRecord.NextRecord = record.NextRecord;
        }

        private void InsertRecord(SequenceRecord record)
        {
		    record.NextRecord = _scope;
            record.PrevRecord = _scope.PrevRecord;
            
			_scope.PrevRecord.NextRecord = record;
            _scope.PrevRecord = record;
        }

        /// <summary>
        /// Determines whether the specified key contains key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public virtual bool ContainsKey(object key)
        {
            return _hashtableRecords.ContainsKey(key);
        }

        /// <summary>
        /// Determines whether the specified value contains value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public virtual bool ContainsValue(object value)
        {
            if (value != null)
            {
                var current = _scope.NextRecord;
                while (current != _scope)
                {
                    if (value.Equals(current.Value))
                        return true;

                    current = current.NextRecord;
                }
            }
            else
            {
                var current = _scope.NextRecord;
                while (current != _scope)
                {
                    if (current.Value == null)
                        return true;

                    current = current.NextRecord;
                }
            }
            return false;
        }

        private bool HasRecords
        {
            get { return _scope.NextRecord != _scope; }
        }

        private SequenceRecord GetFirstRecord()
        {
            return (HasRecords) ? _scope.NextRecord : null;
        }

        private SequenceRecord GetLastRecord()
        {
            return (HasRecords) ? _scope.PrevRecord : null;
        }

        /// <summary>
        /// Gets the first key.
        /// </summary>
        /// <value>
        /// The first key.
        /// </value>
        public virtual object FirstKey
        {
            get
            {
                var first = GetFirstRecord();
                return first != null ? first.Key : null;
            }
        }

        /// <summary>
        /// Gets the first value.
        /// </summary>
        /// <value>
        /// The first value.
        /// </value>
        public virtual object FirstValue
        {
            get
            {
                var first = GetFirstRecord();
                return first != null ? first.Value : null;
            }
        }

        /// <summary>
        /// Gets the last key.
        /// </summary>
        /// <value>
        /// The last key.
        /// </value>
        public virtual object LastKey
        {
            get
            {
                var last = GetLastRecord();
                return last != null ? last.Key : null;
            }
        }

        /// <summary>
        /// Gets the last value.
        /// </summary>
        /// <value>
        /// The last value.
        /// </value>
        public virtual object LastValue
        {
            get
            {
                var last = GetLastRecord();
                return last != null ? last.Value : null;
            }
        }

    }
}
