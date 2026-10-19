using UnityEngine;
namespace Sparc.Original
{
    public static class OriginalComponents
    {
        public static T Ensure<T>(GameObject target) where T:Component
        {var component=target.GetComponent<T>();return component?component:target.AddComponent<T>();}
        /// <summary>Unity re-drives a screen-space overlay canvas to its CanvasScaler factor before it
        /// renders. Match that factor (net of any scaled parent) so raycasts, the read-only probe and
        /// world-space layout measured between frames agree with the drawn pixels. The former
        /// 1/parentScale was only right at a factor of exactly 1 (the 1440x900 test profile).</summary>
        public static void MatchOverlayScale(Canvas canvas)
        {
            if(!canvas)return;var t=canvas.transform;float factor=canvas.rootCanvas?Mathf.Max(.0001f,canvas.rootCanvas.scaleFactor):1;
            var p=t.parent?t.parent.lossyScale:Vector3.one;
            t.localScale=new Vector3(factor/Mathf.Max(.0001f,Mathf.Abs(p.x)),factor/Mathf.Max(.0001f,Mathf.Abs(p.y)),factor/Mathf.Max(.0001f,Mathf.Abs(p.z)));
        }
    }
}
