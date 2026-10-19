using UnityEngine;

namespace Sparc.Original
{
    /// <summary>Source solid-collider contact, sound and particle contract. Push impulses
    /// are integrated by the original player driver, without locking player input.</summary>
    public sealed class OriginalCrowdCollision:MonoBehaviour
    {
        OriginalCrowd owner;OriginalBehaviour source;OriginalPlayer pushing;AudioSource audioSource;Vector3 direction;float began,duration;
        public int Contacts {get;private set;}
        public int Impulses {get;private set;}
        public bool IsPushing=>pushing&&Time.fixedTime-began<duration;
        public void Initialize(OriginalCrowd crowd,OriginalBehaviour configuration)
        {owner=crowd;source=configuration;audioSource=GetComponent<AudioSource>();}
        void OnCollisionEnter(Collision collision)
        {
            if(!source||!source.isActiveAndEnabled||!owner||!owner.runtime||IsPushing)return;
            var player=collision.gameObject.GetComponentInParent<OriginalPlayer>();
            if(!player||player!=owner.runtime.player||!player.CanControl)return;
            Contacts++;owner.Contact();pushing=player;began=Time.fixedTime;duration=Mathf.Max(.02f,source.Number("pushBackDuration",1));
            direction=player.transform.position-transform.position;direction.y=0;
            if(direction.sqrMagnitude<.0001f&&collision.contactCount>0)direction=-collision.GetContact(0).normal;
            direction.y=0;direction=direction.normalized;
            var sound=source.Ref("collisionSound") as AudioClip;
            if(sound){if(!audioSource){audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;}if(!audioSource.isPlaying){audioSource.clip=sound;audioSource.Play();}}
            var point=collision.contactCount>0?collision.GetContact(0).point:transform.position;
            owner.PlayEffect(source.Object("collisionParticlePrefab"),point,direction.sqrMagnitude>.0001f?Quaternion.LookRotation(direction):Quaternion.identity,source.Number("particleLifetime",2),source.Number("particleSizeMultiplier",1));
        }
        void FixedUpdate()
        {
            if(!IsPushing){pushing=null;return;}
            if(!source||!source.isActiveAndEnabled||!owner||!owner.runtime||owner.runtime.ShuttingDown){pushing=null;return;}
            float ratio=1-Mathf.Clamp01((Time.fixedTime-began)/duration);
            pushing.ApplyCrowdImpulse(direction*source.Number("maxPushBackForce",10)*ratio*60*Time.fixedDeltaTime);Impulses++;
        }
        void OnDisable(){pushing=null;}
    }
}
