using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Sparc.Original
{
    public sealed class OriginalCrowdActor:MonoBehaviour
    {
        public OriginalBehaviour Source {get;private set;}
        public bool Spawned {get;private set;}
        public float Lifetime {get;private set;}
        public bool Retiring {get;private set;}
        public int SpawnerId {get;internal set;}
        public bool NavReady=>agent&&agent.enabled&&agent.isOnNavMesh;
        public bool HasPath=>NavReady&&agent.hasPath;
        public float Age=>Time.time-born;
        OriginalCrowd owner;NavMeshAgent agent;float born,originY,nextRecovery,retireAt,spawnDuration,initialScale,targetScale,particleUntil,behaviorUntil,behaviorDuration,nextStuckCheck,stuckSince=-1;
        Vector3 previous;
        bool special,spawnAnimation,navigationAllowed;int waypoint;ParticleSystem spawnParticle;
        readonly List<Vector3> destinations=new();
        public void Initialize(OriginalCrowd crowd,OriginalBehaviour source,bool spawned,float lifetime)
        {
            if(Source)return;owner=crowd;Source=source;Spawned=spawned;Lifetime=lifetime;born=Time.time;originY=transform.position.y;agent=GetComponent<NavMeshAgent>();
            navigationAllowed=agent&&agent.enabled;
            behaviorDuration=spawned?Random.Range(70f,100f):Mathf.Max(.1f,Source.Number("conditionDuration",10));behaviorUntil=Time.time+behaviorDuration;previous=transform.position;
            foreach(var reference in source.Data["Destination_Points"] as JArray??new JArray())
            {var point=OriginalBehaviour.AsGameObject(source.Resolve(reference));if(point)destinations.Add(point.transform.position+new Vector3(Random.Range(-.3f,.3f),0,Random.Range(-.3f,.3f)));}
            for(int i=0;i<destinations.Count;i++){int j=Random.Range(i,destinations.Count);(destinations[i],destinations[j])=(destinations[j],destinations[i]);}
            if(destinations.Count==0)for(int i=0;i<5;i++){var p=transform.position+new Vector3(Random.Range(-5f,5f),0,Random.Range(-5f,5f));if(NavMesh.SamplePosition(p,out var hit,5,NavMesh.AllAreas))destinations.Add(hit.position);}
        }
        public void ConfigureSpawn(float scale,float duration,float initial,bool alternative)
        {targetScale=scale;spawnDuration=Mathf.Max(.02f,duration);initialScale=initial;special=alternative;spawnAnimation=true;transform.localScale=Vector3.one*(scale*initial);}
        public void AllowNavigation(bool allowed){navigationAllowed=allowed;}
        public void StartNavigation()
        {
            if(!navigationAllowed||!agent||!Source||!Source.isActiveAndEnabled)return;
            if(!NavReady&&NavMesh.SamplePosition(transform.position,out var hit,5,NavMesh.AllAreas)){agent.enabled=false;transform.position=hit.position;agent.enabled=true;agent.Warp(hit.position);}
            if(NavReady){agent.isStopped=false;SetDestination();}
        }
        void SetDestination()
        {
            if(!NavReady||destinations.Count==0)return;
            waypoint=(waypoint+1)%destinations.Count;var position=destinations[waypoint];
            if(NavMesh.SamplePosition(position,out var hit,5,NavMesh.AllAreas))position=hit.position;
            agent.SetDestination(position);
        }
        public void StopNavigation(){if(NavReady){agent.isStopped=true;agent.ResetPath();}}
        public void StopSpawnParticle(ParticleSystem particle,float delay){spawnParticle=particle;particleUntil=Time.time+delay;}
        void Update()
        {
            if(!owner||!owner.runtime||!owner.runtime.Started||owner.runtime.ShuttingDown||!Source||!Source.isActiveAndEnabled)return;
            if(spawnParticle&&Time.time>=particleUntil){spawnParticle.Stop();spawnParticle=null;}
            if(Spawned&&Age>=Lifetime&&!Retiring)
            {Retiring=true;retireAt=Time.time+(special?0:1);owner.Expiring(this);}
            if(Retiring&&Time.time>=retireAt){Destroy(gameObject);return;}
            if(spawnAnimation)
            {
                float t=Mathf.Clamp01(Age/spawnDuration);float eased;
                if(t>=1)eased=1;
                else if(special){float u=t-1;eased=1+2.70158f*u*u*u+1.70158f*u*u;}
                else eased=t<=0?0:Mathf.Pow(2,-10*t)*Mathf.Sin((10*t-.75f)*(2*Mathf.PI/3))+1;
                transform.localScale=Vector3.one*Mathf.LerpUnclamped(targetScale*initialScale,targetScale,eased);
                if(t>=1)spawnAnimation=false;
            }
            if(!Spawned&&owner.runtime.visuals.TimelineOwns(transform))return;
            if(!NavReady)
            {if(Time.time>=nextRecovery){nextRecovery=Time.time+1;StartNavigation();}return;}
            if(Time.time>=behaviorUntil){behaviorUntil=Time.time+behaviorDuration;SetDestination();}
            if(Time.time>=nextStuckCheck)
            {
                nextStuckCheck=Time.time+1;
                bool stuck=Vector3.Distance(previous,transform.position)<.1f&&agent.velocity.sqrMagnitude<.01f&&!agent.pathPending&&agent.remainingDistance>Source.Number("distanceThreshold",.5f);previous=transform.position;
                if(stuck&&stuckSince<0)stuckSince=Time.time;else if(!stuck)stuckSince=-1;
            }
            if(stuckSince>=0&&Time.time-stuckSince>1){agent.ResetPath();if(Time.time-stuckSince>2){waypoint=(waypoint+1)%Mathf.Max(1,destinations.Count);SetDestination();stuckSince=-1;}}
            if(!agent.pathPending&&(!agent.hasPath||agent.remainingDistance<Source.Number("distanceThreshold",.5f)))SetDestination();
            if(Source.Flag("shouldFloat",true))
            {var p=transform.position;p.y=originY+Mathf.Sin(Time.time*Source.Number("floatSpeed",.88f))*Source.Number("floatHeight",.5f);transform.position=p;}
        }
        public object Snapshot()
        {
            var collision=GetComponent<OriginalCrowdCollision>();var p=transform.position;
            return new{instanceId=gameObject.GetInstanceID(),sourceId=Source?Source.sourceId:"",spawned=Spawned,position=new{x=p.x,y=p.y,z=p.z},age=Age,lifetime=Lifetime,retiring=Retiring,navReady=NavReady,hasPath=HasPath,pushing=collision&&collision.IsPushing,contacts=collision?collision.Contacts:0};
        }
        void OnDestroy(){if(owner)owner.ActorDestroyed(this);}
    }
}
