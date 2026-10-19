using UnityEngine;
namespace Sparc.Original
{
    /// <summary>The original minigame prefab's physics behavior, not replacement click-to-score gameplay.</summary>
    public sealed class OriginalMiniToken:MonoBehaviour
    {
        public OriginalBehaviour source;public OriginalMiniGame game;public bool Oxygen,Hostile;
        bool collected,joined;Rigidbody2D body;float redirect;Vector2 destination;
        public bool Attached=>joined;
        public void Freeze()
        {
            if(!body)body=GetComponent<Rigidbody2D>();
            if(body){body.linearVelocity=Vector2.zero;body.angularVelocity=0;body.simulated=false;}
        }
        void LateUpdate()
        {
            if(!Oxygen||collected||!game||!game.Area||game.Finished)return;
            // This is an out-of-world safeguard, not a score rule: an oxygen body that
            // tunnels past the original kill plane must not fall forever and consume a spawn slot.
            var local=game.Area.InverseTransformPoint(transform.position);var bounds=game.Area.rect;
            if(local.y<bounds.yMin-bounds.height||local.x<bounds.xMin-bounds.width||local.x>bounds.xMax+bounds.width)
            {collected=true;game.Remove(this);Destroy(gameObject);}
        }

        void Start()
        {
            body=GetComponent<Rigidbody2D>();if(Oxygen&&body)body.gravityScale=source?source.Number("gravityMultiplier",50)*Mathf.Max(0.001f,transform.parent.lossyScale.x):50;
        }
        void FixedUpdate()
        {
            if(Oxygen||collected||!source||!game||game.Finished||game.Transitioning||!body)return;
            var player=game.Player;if(!player)return;
            float scale=Mathf.Max(0.001f,game.Area.lossyScale.x);Vector2 away=(Vector2)transform.position-(Vector2)player.transform.position;
            Vector2 direction;
            if(away.magnitude<source.Number("_detectionRadius",300)*scale)direction=away.normalized;
            else
            {
                if(Time.time>=redirect||Vector2.Distance(destination,transform.position)<source.Number("_targetPositionDistanceThreshold",5)*scale){redirect=Time.time+2;destination=(Vector2)transform.position+Random.insideUnitCircle*source.Number("_wanderRadius",500)*scale;}
                direction=(destination-(Vector2)transform.position).normalized;
            }
            body.linearVelocity=direction*source.Number("_speed",source.Number("speed",100))*scale;
            if(direction.sqrMagnitude>0.001f)transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-90);
            var local=(Vector2)game.Area.InverseTransformPoint(transform.position);var rect=transform as RectTransform;var half=rect?rect.rect.size*0.5f:Vector2.one*24;var r=game.Area.rect;
            var clamped=new Vector2(Mathf.Clamp(local.x,r.xMin+half.x,r.xMax-half.x),Mathf.Clamp(local.y,r.yMin+half.y,r.yMax-half.y));if(clamped!=local){var world=game.Area.TransformPoint(clamped);body.position=world;destination=world;redirect=0;}
        }
        void OnCollisionEnter2D(Collision2D contact)
        {
            // The authored bacteria prefab and White body both have solid colliders.
            // Preserve those colliders for arena obstacles, but process player contact too;
            // waiting exclusively for a trigger lets the solid contact push cells apart first.
            if(!Oxygen)CollectWhiteContact(contact.collider);
        }
        void CollectWhiteContact(Collider2D other)
        {
            if(Oxygen||collected||!game||game.Finished||!other)return;
            if(!other.CompareTag("Player")&&!other.GetComponentInParent<OriginalMiniPlayer>())return;
            collected=true;int score=source?(int)source.Number("scoreForDestroy",1):1;
            var sound=source?source.Ref("destroySound") as AudioClip:null;if(sound)AudioSource.PlayClipAtPoint(sound,Camera.main?Camera.main.transform.position:transform.position,0.5f);
            game.Collect(this,score);Destroy(gameObject);
        }
        void OnTriggerEnter2D(Collider2D other)
        {
            if(collected||!game||game.Finished)return;
            if(Oxygen)
            {
                if(other.CompareTag("OxygenDeposit")){collected=true;game.Collect(this,1);Destroy(gameObject);return;}
                if(other.CompareTag("OxygenKillPlane")){collected=true;game.Remove(this);Destroy(gameObject);return;}
                if(!joined&&(other.CompareTag("Player")||other.CompareTag("Oxygen")))
                {
                    var target=other.attachedRigidbody??other.GetComponentInParent<Rigidbody2D>();if(!target||target==body)return;
                    var joint=gameObject.AddComponent<RelativeJoint2D>();joint.connectedBody=target;joint.enableCollision=true;float scale=Mathf.Max(0.001f,transform.parent.lossyScale.x);joint.maxForce=12500*scale;joint.maxTorque=10000*scale;joint.breakForce=13000*scale;joint.breakTorque=10500*scale;joined=true;if(game.Visuals&&(other.CompareTag("Player")||other.GetComponentInParent<OriginalMiniPlayer>()))game.Visuals.Pickup(transform.position);
                }
            }
            else CollectWhiteContact(other);
        }
        void OnDestroy(){if(game)game.Remove(this);}
    }
}
