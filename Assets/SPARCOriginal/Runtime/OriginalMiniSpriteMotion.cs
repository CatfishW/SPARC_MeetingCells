using UnityEngine;

namespace Sparc.Original
{
    /// <summary>Animation belongs to the visible art; native bodies, joints and hitboxes never scale.</summary>
    public sealed class OriginalMiniSpriteMotion:MonoBehaviour
    {
        public RectTransform Visual {get;private set;}
        public string Role {get;private set;}
        Rigidbody2D body;UnityEngine.UI.Image image;float phase,pulseAt=-10;bool positive=true;
        public void Bind(UnityEngine.UI.Image target,string role)
        {Visual=target.rectTransform;image=target;Role=role;body=GetComponent<Rigidbody2D>();phase=(Mathf.Abs(GetInstanceID())%29)*.37f;}
        public void Pulse(bool success)
        {pulseAt=Time.unscaledTime;positive=success;}
        void LateUpdate()
        {
            if(!Visual||!image)return;float time=Time.unscaledTime,breath=Mathf.Sin(time*2.6f+phase),wiggle=Mathf.Sin(time*6+phase);
            float speed=body?Mathf.Clamp(body.linearVelocity.x/Mathf.Max(.01f,transform.lossyScale.x*350),-1,1):0;
            float kick=Mathf.Clamp01(1-(time-pulseAt)/.42f);kick=kick*kick*Mathf.Sin((time-pulseAt)*22);
            float amount=Role.Contains("bacterium")?.035f:.018f;
            Visual.localScale=new Vector3(1+breath*amount+kick*.10f,1-breath*amount-kick*.07f,1);
            Visual.localRotation=Quaternion.Euler(0,0,-speed*7+(Role.Contains("bacterium")?wiggle*4:breath*1.5f));
            Visual.anchoredPosition=new Vector2(0,breath*(Role.Contains("oxygen")?2.5f:1.6f));
            image.color=Color.Lerp(Color.white,positive?new Color(.69f,1,.92f):new Color(1,.55f,.43f),Mathf.Clamp01(1-(time-pulseAt)/.36f)*.6f);
        }
    }
}
