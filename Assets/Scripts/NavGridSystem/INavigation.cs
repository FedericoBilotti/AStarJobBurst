using Unity.Jobs;
using UnityEngine;

namespace NavGridSystem
{
    public interface INavigation
    {
        void RequestPath(Vector3 start, Vector3 end);
    }
}