using UnityEngine;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>New implementation, original MonoScript GUID and animated field contract.
    /// Keeping this GUID lets the original embedded Timeline curves bind without editing those assets.</summary>
    public sealed class CutoutCircleAnimationControl:MonoBehaviour
    {
        [Range(0,1)] public float cutoutSize=0.5f;
        public GameObject cutoutCircleObject;
        Material instance,original;Graphic graphic;Renderer mesh;
        void OnEnable()
        {
            if(instance)return;
            if(!cutoutCircleObject)return;
            graphic=cutoutCircleObject.GetComponent<Graphic>();mesh=cutoutCircleObject.GetComponent<Renderer>();
            original=graphic?graphic.material:mesh?mesh.sharedMaterial:null;
            if(!original)return;instance=new Material(original){name=original.name+" / owned animated cutout"};
            if(graphic)graphic.material=instance;else mesh.sharedMaterial=instance;
        }
        void LateUpdate(){if(instance)instance.SetFloat("_CutoutSize",cutoutSize);}
        void OnDidApplyAnimationProperties(){if(instance)instance.SetFloat("_CutoutSize",cutoutSize);}
        void OnDestroy(){if(graphic&&graphic.material==instance)graphic.material=original;else if(mesh&&mesh.sharedMaterial==instance)mesh.sharedMaterial=original;if(instance)Destroy(instance);}
    }
}
