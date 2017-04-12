using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial struct ModelMerger<T>
    {
        //參考 System.Linq.Set<TElement> 改寫
        //ref: https://github.com/dotnet/corefx/blob/master/src/System.Linq/src/System/Linq/Set.cs


        /// <summary>
        /// A lightweight hash set.
        /// </summary>
        /// <typeparam name="TElement">The type of the set's items.</typeparam>
        private sealed class Set
        {
            /// <summary>
            /// The comparer used to hash and compare items in the set.
            /// </summary>
            private readonly IEqualityComparer<T> _comparer;

            /// <summary>
            /// The hash buckets, which are used to index into the slots.
            /// </summary>
            private int[] _buckets;

            /// <summary>
            /// The slots, each of which store an item and its hash code.
            /// </summary>
            private Slot[] _slots;

            /// <summary>
            /// The number of items in this set.
            /// </summary>
            private int _count;

            /// <summary>
            /// Constructs a set that compares items with the specified comparer.
            /// </summary>
            /// <param name="comparer">
            /// The comparer. If this is <c>null</c>, it defaults to <see cref="EqualityComparer{TElement}.Default"/>.
            /// </param>
            public Set(IEqualityComparer<T> comparer)
            {
                _comparer = comparer ?? EqualityComparer<T>.Default;
                _buckets = new int[7];
                _slots = new Slot[7];
            }

            /// <summary>
            /// Attempts to add an item to this set.
            /// </summary>
            /// <param name="value">The item to add.</param>
            /// <returns>
            /// <c>true</c> if the item was not in the set; otherwise, <c>false</c>.
            /// </returns>
            public bool Add(T value)
            {
                int hashCode = InternalGetHashCode(value);
                for (int i = _buckets[hashCode % _buckets.Length] - 1; i >= 0; i = _slots[i]._next)
                {
                    if (_slots[i]._hashCode == hashCode && _comparer.Equals(_slots[i]._value, value))
                    {
                        return false;
                    }
                }

                if (_count == _slots.Length)
                {
                    Resize();
                }

                int index = _count;
                _count++;
                int bucket = hashCode % _buckets.Length;
                _slots[index]._hashCode = hashCode;
                _slots[index]._value = value;
                _slots[index]._next = _buckets[bucket] - 1;
                _buckets[bucket] = index + 1;
                return true;
            }

            /// <summary>
            /// Attempts to remove an item from this set.
            /// </summary>
            /// <param name="value">The item to remove.</param>
            /// <returns>
            /// <c>true</c> if the item was in the set; otherwise, <c>false</c>.
            /// </returns>
            public bool Remove(T value)
            {
                int hashCode = InternalGetHashCode(value);
                int bucket = hashCode % _buckets.Length;
                int last = -1;
                for (int i = _buckets[bucket] - 1; i >= 0; last = i, i = _slots[i]._next)
                {
                    if (_slots[i]._hashCode == hashCode && _comparer.Equals(_slots[i]._value, value))
                    {
                        if (last < 0)
                        {
                            _buckets[bucket] = _slots[i]._next + 1;
                        }
                        else
                        {
                            _slots[last]._next = _slots[i]._next;
                        }

                        _slots[i]._hashCode = -1;
                        _slots[i]._value = default(T);
                        _slots[i]._next = -1;
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Expands the capacity of this set to double the current capacity, plus one.
            /// </summary>
            private void Resize()
            {
                int newSize = checked((_count * 2) + 1);
                int[] newBuckets = new int[newSize];
                Slot[] newSlots = new Slot[newSize];
                Array.Copy(_slots, 0, newSlots, 0, _count);
                for (int i = 0; i < _count; i++)
                {
                    int bucket = newSlots[i]._hashCode % newSize;
                    newSlots[i]._next = newBuckets[bucket] - 1;
                    newBuckets[bucket] = i + 1;
                }

                _buckets = newBuckets;
                _slots = newSlots;
            }

            /// <summary>
            /// Creates an array from the items in this set.
            /// </summary>
            /// <returns>An array of the items in this set.</returns>
            public T[] ToArray()
            {
                T[] array = new T[_count];
                for (int i = 0; i != array.Length; ++i)
                {
                    array[i] = _slots[i]._value;
                }

                return array;
            }

            /// <summary>
            /// Creates a list from the items in this set.
            /// </summary>
            /// <returns>A list of the items in this set.</returns>
            public List<T> ToList()
            {
                int count = _count;
                List<T> list = new List<T>(count);
                for (int i = 0; i != count; ++i)
                {
                    list.Add(_slots[i]._value);
                }

                return list;
            }

            /// <summary>
            /// The number of items in this set.
            /// </summary>
            public int Count => _count;

            /// <summary>
            /// Gets the hash code of the provided value with its sign bit zeroed out, so that modulo has a positive result.
            /// </summary>
            /// <param name="value">The value to hash.</param>
            /// <returns>The lower 31 bits of the value's hash code.</returns>
            private int InternalGetHashCode(T value)
            {
                // Handle comparer implementations that throw when passed null
                return (value == null) ? 0 : _comparer.GetHashCode(value) & 0x7FFFFFFF;
            }

            /// <summary>
            /// An entry in the hash set.
            /// </summary>
            internal struct Slot
            {
                /// <summary>
                /// The hash code of the item.
                /// </summary>
                internal int _hashCode;

                /// <summary>
                /// In the case of a hash collision, the index of the next slot to probe.
                /// </summary>
                internal int _next;

                /// <summary>
                /// The item held by this slot.
                /// </summary>
                internal T _value;
            }
        }
    }
}
