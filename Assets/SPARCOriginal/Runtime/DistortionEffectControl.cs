using UnityEngine;
using UnityEngine.UI;
namespace Sparc.Original
{
    /// <summary>Preserves the authored animation binding identity, not the old implementation.</summary>
    public sealed class DistortionEffectControl:MonoBehaviour
    {
        public GameObject materialSource;
        public float distortionAmount=0.1f,distortionSpeed=1;
        Material copy,original;RawImage image;
        void OnEnable(){if(copy||!materialSource)return;image=materialSource.GetComponent<RawImage>();if(!image||!image.material)return;original=image.material;copy=new Material(original);image.material=copy;}
        void LateUpdate(){if(copy){copy.SetFloat("_DistortionAmount",distortionAmount);copy.SetFloat("_DistortionSpeed",distortionSpeed);}}
        void OnDestroy(){if(image&&image.material==copy)image.material=original;if(copy)Destroy(copy);}
    }
}
