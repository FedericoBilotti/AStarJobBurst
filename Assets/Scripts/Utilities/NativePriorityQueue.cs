using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;

namespace Utilities
{
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativePriorityQueueDebugView<>))]
    public unsafe struct NativePriorityQueue<T> : IDisposable where T : unmanaged, IHeapComparable<T>
    {
        [NativeDisableUnsafePtrRestriction] 
        internal void* m_Buffer;
        internal int m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal int m_MinIndex;
        internal int m_MaxIndex;
        internal AtomicSafetyHandle m_Safety;
        internal static readonly SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativePriorityQueue<T>>();
#endif

        internal Allocator m_AllocatorLabel;

        public NativePriorityQueue(int length, Allocator allocator)
        {
            int totalSize = UnsafeUtility.SizeOf<T>() * length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, TempJob, Persistent or registered custom allocator
            if (allocator <= Allocator.None) throw new ArgumentException("Allocator must be Temp, TempJob, Persistent or registered custom allocator", nameof(allocator));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be >= 0");
            if (!UnsafeUtility.IsBlittable<T>()) throw new ArgumentException(string.Format("{0} used in NativePriorityQueue<{0}> must be blittable", typeof(T)));
#endif

            m_Buffer = AllocatorManager.Allocate(allocator, totalSize, UnsafeUtility.AlignOf<T>());
            UnsafeUtility.MemClear(m_Buffer, totalSize);

            m_Length = 0;
            m_AllocatorLabel = allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = length - 1;
            m_Safety = CollectionHelper.CreateSafetyHandle(allocator);
            CollectionHelper.SetStaticSafetyId<NativePriorityQueue<T>>(ref m_Safety, ref s_staticSafetyId.Data);
#endif
        }

        public int Length => m_Length;
        public bool IsCreated => m_Buffer != null;

        public T this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // If the container is currently not allowed to read from the buffer then this will throw an exception.
                // This handles all cases, from already disposed containers
                // to safe multithreaded access.
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);

                // Perform out of range checks based on
                // the NativeContainerSupportsMinMaxWriteRestriction policy
                if (index < m_MinIndex || index > m_MaxIndex) FailOutOfRangeError(index);
#endif
                // Read the element from the allocated native memory
                return UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
            }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);

                if (index < m_MinIndex || index > m_MaxIndex) FailOutOfRangeError(index);
#endif
                // Writes value to the allocated native memory
                UnsafeUtility.WriteArrayElement(m_Buffer, index, value);
            }
        }

        public void Enqueue(T item)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

            item.HeapIndex = m_Length;
            UnsafeUtility.WriteArrayElement(m_Buffer, m_Length, item);
            SortUp(m_Length);
            m_Length++;
        }

        private void SortUp(int index)
        {
            int parentIndex = GetParent(index);
                
            T currentValue = UnsafeUtility.ReadArrayElement<T>(m_Buffer, index);
            T parentValue = UnsafeUtility.ReadArrayElement<T>(m_Buffer, parentIndex);
            
            while (index > 0 && currentValue.CompareTo(parentValue) < 0)
            {
                Swap(index, parentIndex);
                index = parentIndex;
                parentIndex = GetParent(index);
                parentValue = UnsafeUtility.ReadArrayElement<T>(m_Buffer, parentIndex);
            }
        }

        public T Dequeue()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif

            if (m_Length <= 0)
                throw new InvalidOperationException("La cola de prioridad está vacía.");

            T firstItem = this[0];
            m_Length--;
            
            if (m_Length <= 0) return firstItem; // There isn't any element to sort

            T lastElement = this[m_Length];
            lastElement.HeapIndex = 0;
            this[0] = lastElement;
            SortDown();
            return firstItem;
        }

        private void SortDown()
        {
            int index = 0;
            int changeIndex = index;
            
            while (true)
            {
                int leftChildIndex = LeftChild(index);
                int rightChildIndex = RightChild(index);
                
                if (leftChildIndex < Length && this[changeIndex].CompareTo(this[leftChildIndex]) > 0)
                {
                    changeIndex = leftChildIndex;
                }
                
                if (rightChildIndex < Length && this[changeIndex].CompareTo(this[rightChildIndex]) > 0)
                {
                    changeIndex = rightChildIndex;
                }

                if (index == changeIndex) break;

                Swap(index, changeIndex);
                index = changeIndex;
            }
        }

        private void Swap(int indexA, int indexB)
        {            
            var xElement = UnsafeUtility.ReadArrayElement<T>(m_Buffer, indexA);
            this[indexA] = this[indexB];
            this[indexB] = xElement;
        }

        public T Peek()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

            if (m_Length <= 0)
                throw new InvalidOperationException("La cola de prioridad está vacía.");

            return UnsafeUtility.ReadArrayElement<T>(m_Buffer, 0);
        }
        
        private static int GetParent(int i) => (i - 1) / 2;
        private static int LeftChild(int i) => 2 * i + 1;
        private static int RightChild(int i) => 2 * i + 2;

        public bool Contains(T item)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            for (int i = 0; i < m_Length; i++)
            {
                if (this[i].HeapIndex == item.HeapIndex) 
                    return true;
            }
            
            return false;
        }

        public T[] ToArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

            var array = new T[Length];
            for (var i = 0; i < Length; i++) array[i] = UnsafeUtility.ReadArrayElement<T>(m_Buffer, i);
            return array;
        }
        
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif

            AllocatorManager.Free(m_AllocatorLabel, m_Buffer);
            m_Buffer = null;
            m_Length = 0;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private void FailOutOfRangeError(int index)
        {
            if (index < Length && (m_MinIndex != 0 || m_MaxIndex != Length - 1))
                throw new IndexOutOfRangeException(string.Format(
                        "HeapIndex {0} is out of restricted IJobParallelFor range [{1}...{2}] in ReadWriteBuffer.\n" + "ReadWriteBuffers are restricted to only read & write the element at the job index. " +
                        "You can use double buffering strategies to avoid race conditions due to " + "reading & writing in parallel to the same elements from a job.", index, m_MinIndex, m_MaxIndex));

            throw new IndexOutOfRangeException(string.Format("HeapIndex {0} is out of range of '{1}' Length.", index, Length));
        }

#endif
    }

    internal sealed class NativePriorityQueueDebugView<T> where T : unmanaged, IHeapComparable<T>
    {
        private NativePriorityQueue<T> m_Array;

        public NativePriorityQueueDebugView(NativePriorityQueue<T> array)
        {
            m_Array = array;
        }

        public T[] Items => m_Array.ToArray();
    }
}

public interface IHeapComparable<in T> : IComparable<T>
{
    public int HeapIndex { get; set;  }
}