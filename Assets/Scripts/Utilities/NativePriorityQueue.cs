using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor.Experimental.GraphView;
using Debug = UnityEngine.Debug;

namespace Utilities
{
    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [NativeContainerSupportsMinMaxWriteRestriction]
    [NativeContainerSupportsDeallocateOnJobCompletion]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativePriorityQueueDebugView<>))]
    public unsafe struct NativePriorityQueue<T> : INativeDisposable where T : unmanaged, IHeapComparable<T>
    {
        [NativeDisableUnsafePtrRestriction] 
        internal void* m_Buffer;
        internal int m_Length;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal int m_MinIndex;
        internal int m_MaxIndex;
        internal AtomicSafetyHandle m_Safety;
        internal static SharedStatic<int> s_staticSafetyId = SharedStatic<int>.GetOrCreate<NativePriorityQueue<T>>();
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
            
            m_Buffer = UnsafeUtility.MallocTracked(totalSize, UnsafeUtility.AlignOf<T>(), allocator, 1);
            UnsafeUtility.MemClear(m_Buffer, totalSize);

            m_Length = 0;
            m_AllocatorLabel = allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_MinIndex = 0;
            m_MaxIndex = length;
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
            
            if (Length >= m_MaxIndex)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
#endif
                int newCapacity = m_MaxIndex * 2;
                void* newBuffer = UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<T>() * newCapacity, UnsafeUtility.AlignOf<T>(), m_AllocatorLabel, 1);
                
                UnsafeUtility.MemCpy(newBuffer, m_Buffer, UnsafeUtility.SizeOf<T>() * Length);
                UnsafeUtility.FreeTracked(m_Buffer, m_AllocatorLabel);

                m_Buffer = newBuffer;
                m_MaxIndex = newCapacity;
            }
            
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
            var aIndex = UnsafeUtility.ReadArrayElement<T>(m_Buffer, indexA);
            this[indexA] = this[indexB];
            this[indexB] = aIndex;
        }

        public T Peek()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif

            if (m_Length <= 0)
                throw new InvalidOperationException("The priority queue is empty.");

            return UnsafeUtility.ReadArrayElement<T>(m_Buffer, 0);
        }
        
        private static int GetParent(int i) => (i - 1) / 2;
        private static int LeftChild(int i) => 2 * i + 1;
        private static int RightChild(int i) => 2 * i + 2;
        
        public void Clear() => m_Length = 0;

        /// <summary>
        /// Check if the item is in the priority queue. O(1)
        /// </summary>
        /// <param name="item">The item to check</param>
        /// <returns></returns>
        public bool Contains(T item)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            if (item.HeapIndex < 0 || item.HeapIndex > m_Length - 1) return false;
            
            // If the item of the HeapIndex is equal to the current item, return true. Otherwise, return false.
            int heapIndex = item.HeapIndex;
            return heapIndex == this[heapIndex].HeapIndex;
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
        
        [WriteAccessRequired]
        public void Dispose()
        {
            if (m_AllocatorLabel != Allocator.None && !AtomicSafetyHandle.IsDefaultValue(in m_Safety)) 
                AtomicSafetyHandle.CheckExistsAndThrow(in m_Safety);
            
            if (!IsCreated) 
                return;
            
            if (m_AllocatorLabel == Allocator.Invalid) 
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was not allocated with a valid allocator.");
            
            if (m_AllocatorLabel >= Allocator.FirstUserIndex) 
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was allocated with a custom allocator, use CollectionHelper.Dispose in com.unity.collections package.");
            
            if (m_AllocatorLabel > Allocator.None)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                CollectionHelper.DisposeSafetyHandle(ref m_Safety);
#endif
                UnsafeUtility.FreeTracked(m_Buffer, m_AllocatorLabel);
                m_AllocatorLabel = Allocator.Invalid;
            }
            
            m_Buffer = null;
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (m_AllocatorLabel != Allocator.None && !AtomicSafetyHandle.IsDefaultValue(in m_Safety))
                AtomicSafetyHandle.CheckExistsAndThrow(in m_Safety);
            
            if (!IsCreated)
                return inputDeps;
            
            if (m_AllocatorLabel >= Allocator.FirstUserIndex)
                throw new InvalidOperationException("The NativeArray can not be Disposed because it was allocated with a custom allocator, use CollectionHelper.Dispose in com.unity.collections package.");
            
            if (m_AllocatorLabel > Allocator.None)
            {
                JobHandle jobHandle = new NativePriorityQueueDisposeJob<T>
                {
                    Data = new NativePriorityQueueDispose<T>
                    {
                        m_Buffer = m_Buffer,
                        m_AllocatorLabel = m_AllocatorLabel,
                        m_Safety = m_Safety
                    }
                }.Schedule(inputDeps);
                
                AtomicSafetyHandle.Release(m_Safety);
                m_Buffer = null;
                m_AllocatorLabel = Allocator.Invalid;
                return jobHandle;
            }
            m_Buffer = null;
            return inputDeps;
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