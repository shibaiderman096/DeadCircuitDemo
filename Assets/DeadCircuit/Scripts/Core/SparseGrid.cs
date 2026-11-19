using System.Collections.Generic;

namespace DeadCircuit
{
    internal sealed class SparseGrid<T> where T:class
    {
        readonly Dictionary<long,T> cells=new Dictionary<long,T>();
        public T this[long key]
        {
            get { T value; return cells.TryGetValue(key,out value) ? value : null; }
            set { if(value==null) cells.Remove(key); else cells[key]=value; }
        }
        public void Clear() => cells.Clear();
    }
    internal sealed class FlowGrid
    {
        readonly Dictionary<long,int> cells=new Dictionary<long,int>();
        public int this[long key]
        {
            get { int value; return cells.TryGetValue(key,out value) ? value : int.MaxValue; }
            set { cells[key]=value; }
        }
        public void Clear() => cells.Clear();
    }
}
