using System;
using Unity.Collections;

namespace Utilities
{
    public static class NativeListExtensions
    {
        public static void Reverse<T>(this NativeList<T> list) where T : unmanaged, IEquatable<T>
        {
            int length = list.Length;
            for (int i = 0; i < length / 2; i++)
            {
                (list[i], list[length - i - 1]) = (list[length - i - 1], list[i]);
            }
        }
    }
}