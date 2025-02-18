using System;

public interface IPriority<in T> : IComparable<T>
{
    public int Priority { get; set; }
}