using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Utilities
{
    [NativeContainer]
    internal struct NativePriorityQueueDispose<T> where T : unmanaged, IComparable<T>
    {
        [NativeDisableUnsafePtrRestriction]
        internal unsafe void* m_Buffer;
        internal Allocator m_AllocatorLabel;
        internal AtomicSafetyHandle m_Safety;

        public unsafe void Dispose() => UnsafeUtility.FreeTracked(m_Buffer, m_AllocatorLabel);
    }
}
