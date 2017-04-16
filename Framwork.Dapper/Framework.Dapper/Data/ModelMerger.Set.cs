using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Data
{
    partial class ModelMerger<T>
    {
        //參考 System.Linq.Set<TElement> 改寫
        //ref: https://github.com/dotnet/corefx/blob/master/src/System.Linq/src/System/Linq/Set.cs

        private sealed class Set
        {
            private struct Slot
            {
                /// <summary>The hash code of the item.</summary>
                internal int hashCode;
                /// <summary>In the case of a hash collision, the index of the next slot to probe.</summary>
                internal int next;
                /// <summary>The item held by this slot.</summary>
                internal T value;
            }
            private readonly IEqualityComparer<T> comparer; //The comparer used to hash and compare items in the set.
            private int[] buckets;  //The hash buckets, which are used to index into the slots.
            private Slot[] slots;  //The slots, each of which store an item and its hash code.
            /// <summary>The number of items in this set.</summary>
            public int Count { get; private set; }

            /// <summary>
            /// Constructs a set that compares items with the specified comparer.
            /// </summary>
            /// <param name="comparer">
            /// The comparer. If this is <c>null</c>, it defaults to <see cref="EqualityComparer{TElement}.Default"/>.
            /// </param>
            public Set(IEqualityComparer<T> comparer) : this(null, comparer) { }

            public Set(IEnumerable<T> collection, IEqualityComparer<T> comparer)
            {
                this.comparer = comparer ?? EqualityComparer<T>.Default;
                var capacity = (collection as ICollection<T>)?.Count ?? 7; //預設7個
                buckets = new int[capacity];
                slots = new Slot[capacity];

                if (collection != null)
                {
                    foreach (var n in collection)
                    {
                        Add(n);
                    }
                }
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
                for (int i = buckets[hashCode % buckets.Length] - 1; i >= 0; i = slots[i].next)
                {
                    if (slots[i].hashCode == hashCode && comparer.Equals(slots[i].value, value)) return false;
                }

                if (Count == slots.Length) 
                {
                    Resize();
                }

                int index = Count;
                Count++;
                int bucket = hashCode % buckets.Length;
                slots[index].hashCode = hashCode;
                slots[index].value = value;
                slots[index].next = buckets[bucket] - 1;
                buckets[bucket] = index + 1;
                return true;
            }

            /// <summary>
            /// Attempts to remove an item from this set.
            /// </summary>
            /// <param name="identity">The identity to remove.</param>
            /// <param name="value">removed item.</param>
            /// <returns>
            /// <c>true</c> if the item was in the set; otherwise, <c>false</c>.
            /// </returns>
            public bool Remove(T identity, out T value)
            {
                int hashCode = InternalGetHashCode(identity);
                int bucket = hashCode % buckets.Length;
                int last = -1;
                for (int i = buckets[bucket] - 1; i >= 0; last = i, i = slots[i].next)
                {
                    if (slots[i].hashCode == hashCode && comparer.Equals(slots[i].value, identity))
                    {
                        if (last < 0)
                        {
                            buckets[bucket] = slots[i].next + 1;
                        }
                        else
                        {
                            slots[last].next = slots[i].next;
                        }

                        slots[i].hashCode = -1;
                        value = slots[i].value;
                        slots[i].value = default(T);
                        slots[i].next = -1;
                        Count--;

                        return true;
                    }
                }
                value = default(T);
                return false;
            }

            /// <summary>
            /// Gets the hash code of the provided value with its sign bit zeroed out, so that modulo has a positive result.
            /// </summary>
            /// <param name="value">The value to hash.</param>
            /// <returns>The lower 31 bits of the value's hash code.</returns>
            private int InternalGetHashCode(T value)
            {
                // Handle comparer implementations that throw when passed null
                return (value == null) ? 0 : comparer.GetHashCode(value) & 0x7FFFFFFF;
            }

            /// <summary>
            /// Expands the capacity of this set to double the current capacity, plus one.
            /// </summary>
            private void Resize()
            {
                int newSize = checked((Count * 2) + 1);
                int[] newBuckets = new int[newSize];
                Slot[] newSlots = new Slot[newSize];
                Array.Copy(slots, 0, newSlots, 0, Count);
                for (int i = 0; i < Count; i++)
                {
                    int bucket = newSlots[i].hashCode % newSize;
                    newSlots[i].next = newBuckets[bucket] - 1;
                    newBuckets[bucket] = i + 1;
                }

                buckets = newBuckets;
                slots = newSlots;
            }

            public List<T> ToList()
            {
                var list = new List<T>(Count);
                if (Count == 0) return list;
                foreach (var n in slots)
                {
                    if (n.hashCode >= 0)
                    {
                        list.Add(n.value);
                        if (list.Count >= Count) break;
                    }
                }
                return list;
            }
        }
    }
}
