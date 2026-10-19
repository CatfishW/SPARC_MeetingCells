using UnityEngine;
namespace Sparc.Original
{
    public static class OriginalComponents
    {
        public static T Ensure<T>(GameObject target) where T:Component
        {var component=target.GetComponent<T>();return component?component:target.AddComponent<T>();}
    }
}
