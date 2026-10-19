using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Sparc.Original
{
    /// <summary>Restores the original enabled NPC_spawn and its scene-instance blocking cells.
    /// The alternative RBC_SPAWN_MeetingCells remains governed by its original enabled state.</summary>
    public sealed class OriginalCrowd : MonoBehaviour
    {
        public OriginalRuntime runtime;
        sealed class Spawner
        {
            public OriginalBehaviour source;
            public float remaining;
            public int current;
            public bool special;
        }
        sealed class Effect { public GameObject gameObject; public float expires; }
        readonly Dictionary<int,Spawner> spawners=new();
        readonly List<OriginalCrowdActor> actors=new();
        readonly List<Effect> effects=new();
        Transform staging;
        public int Spawned {get;private set;}
        public int Expired {get;private set;}
        public int Contacts {get;private set;}
        public int RejectedSpawns {get;private set;}
        public int Current {get {int count=0;foreach(var actor in actors)if(actor&&actor.Spawned)count++;return count;}}
        public int ActiveEffects=>effects.Count;
        public IReadOnlyList<OriginalCrowdActor> Actors=>actors;
        public bool IsOwned(OriginalBehaviour component)=>component&&component.GetComponentInParent<OriginalCrowdOwned>(true);
        public void Initialize()
        {foreach(var source in runtime.components)if(source&&(source.sourceType=="NPC_spawn"||source.sourceType=="RBC_SPAWN_MeetingCells"))Activated(source);}
        void EnsureStaging()
        {
            if(staging)return;
            var root=new GameObject("SPARC crowd / inactive staging");root.transform.SetParent(transform,false);root.SetActive(false);staging=root.transform;
        }
        public void Activated(OriginalBehaviour source)
        {
            if(!source)return;
            switch(source.sourceType)
            {
                case "NPC_spawn":case "RBC_SPAWN_MeetingCells":
                    if(!spawners.ContainsKey(source.GetInstanceID()))spawners.Add(source.GetInstanceID(),new Spawner{source=source,special=source.sourceType=="RBC_SPAWN_MeetingCells",remaining=source.sourceType=="RBC_SPAWN_MeetingCells"?0:Mathf.Max(.02f,source.Number("spawm_time_value",2))});
                    break;
                case "NPC_random_walk":
                    var actor=source.GetComponent<OriginalCrowdActor>();
                    if(!actor){actor=source.gameObject.AddComponent<OriginalCrowdActor>();actor.Initialize(this,source,false,0);actors.Add(actor);}
                    break;
                case "NPC_collision_with_player":
                    var collision=source.GetComponent<OriginalCrowdCollision>()??source.gameObject.AddComponent<OriginalCrowdCollision>();collision.Initialize(this,source);
                    break;
            }
        }
        void Update()
        {
            if(!runtime||!runtime.Started||runtime.ShuttingDown)return;
            foreach(var state in spawners.Values)
            {
                var source=state.source;if(!source||!source.isActiveAndEnabled)continue;
                int cap=Mathf.Clamp((int)source.Number("max_amount_of_NPC",50),0,128);
                if(state.current>=cap)continue;
                state.remaining-=Time.deltaTime;if(state.remaining>0)continue;
                Spawn(state);state.remaining=Mathf.Max(.02f,source.Number("spawm_time_value",2));
            }
            for(int i=effects.Count-1;i>=0;i--)if(!effects[i].gameObject||Time.time>=effects[i].expires){if(effects[i].gameObject)Destroy(effects[i].gameObject);effects.RemoveAt(i);}
        }
        bool SpawnPosition(OriginalBehaviour source,out Transform point,out Vector3 position)
        {
            point=null;position=default;
            var references=source.Data["spawnpoints"] as JArray;if(references==null||references.Count==0)return false;
            int first=UnityEngine.Random.Range(0,references.Count);
            // Try the authored points, never an invented location. Rebaked WebGL surfaces
            // may need the same five-unit projection used by the source waypoint walker.
            for(int n=0;n<references.Count;n++)
            {
                var target=OriginalBehaviour.AsGameObject(source.Resolve(references[(first+n)%references.Count]));if(!target)continue;
                var proposed=target.transform.position+new Vector3(UnityEngine.Random.Range(-1f,1f),0,UnityEngine.Random.Range(-1f,1f));
                if(NavMesh.SamplePosition(proposed,out var hit,1,NavMesh.AllAreas)||NavMesh.SamplePosition(proposed,out hit,5,NavMesh.AllAreas))
                {point=target.transform;position=hit.position;return true;}
            }
            return false;
        }
        void Spawn(Spawner state)
        {
            var source=state.source;var template=source.Object("NPC_for_continuous_spawn");
            if(!template||!SpawnPosition(source,out var point,out var position)){RejectedSpawns++;return;}
            EnsureStaging();
            // Clone the original SCENE instance, including its scene-added collider,
            // NavMeshAgent, collision settings and external authored destination references.
            var clone=Instantiate(template,staging);clone.AddComponent<OriginalCrowdOwned>();
            var agent=clone.GetComponent<NavMeshAgent>();bool navigation=agent&&agent.enabled;if(agent)agent.enabled=false;
            clone.transform.SetPositionAndRotation(position,Quaternion.identity);
            float scale=UnityEngine.Random.Range(.16f,.18f);
            var walker=clone.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType=="NPC_random_walk");
            if(!walker){Destroy(clone);RejectedSpawns++;return;}
            var actor=clone.GetComponent<OriginalCrowdActor>()??clone.AddComponent<OriginalCrowdActor>();
            float lifetime=state.special?Mathf.Max(.1f,source.Number("npcLifetime",10)):UnityEngine.Random.Range(20f,60f);
            actor.Initialize(this,walker,true,lifetime);actor.SpawnerId=source.GetInstanceID();
            actor.AllowNavigation(navigation);
            actor.ConfigureSpawn(scale,state.special ? .5f : source.Number("spawnAnimDuration",.6f),state.special?0:source.Number("initialScale",.01f),state.special);
            var collisionSource=clone.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType=="NPC_collision_with_player");
            if(collisionSource)(clone.GetComponent<OriginalCrowdCollision>()??clone.AddComponent<OriginalCrowdCollision>()).Initialize(this,collisionSource);
            actors.Add(actor);state.current++;Spawned++;
            clone.transform.SetParent(point,true);clone.SetActive(true);
            if(agent&&navigation)
            {
                if(state.special){agent.avoidancePriority=UnityEngine.Random.Range(1,99);agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;agent.acceleration=10;agent.speed*=UnityEngine.Random.Range(.9f,1.1f);}
                agent.enabled=true;agent.Warp(position);
            }
            actor.StartNavigation();
            var particle=clone.GetComponentInChildren<ParticleSystem>();if(particle){particle.Play();actor.StopSpawnParticle(particle,.666f);}
        }
        internal void ActorDestroyed(OriginalCrowdActor actor)
        {
            actors.Remove(actor);
            if(actor.Spawned&&spawners.TryGetValue(actor.SpawnerId,out var state))state.current=Mathf.Max(0,state.current-1);
        }
        internal void Expiring(OriginalCrowdActor actor)
        {
            Expired++;
            var source=actor.Source;GameObject effect=source?source.Object("destroy_effect"):null;
            if(spawners.TryGetValue(actor.SpawnerId,out var state)&&state.special)effect=state.source.Object("destroyParticleEffect");
            PlayEffect(effect,actor.transform.position,Quaternion.identity,2,1);
        }
        internal void Contact()=>Contacts++;
        internal void PlayEffect(GameObject prefab,Vector3 position,Quaternion rotation,float lifetime,float scale)
        {
            if(!prefab||effects.Count>=16||!runtime||runtime.ShuttingDown)return;
            EnsureStaging();var clone=Instantiate(prefab,staging);clone.AddComponent<OriginalCrowdOwned>();
            clone.transform.SetPositionAndRotation(position,rotation);clone.transform.SetParent(transform,true);clone.SetActive(true);
            foreach(var system in clone.GetComponentsInChildren<ParticleSystem>(true)){var main=system.main;main.startSizeMultiplier*=scale;system.Play();}
            effects.Add(new Effect{gameObject=clone,expires=Time.time+Mathf.Clamp(lifetime,.1f,10)});
        }
        public object Snapshot()=>new
        {
            active=Current,spawned=Spawned,expired=Expired,contacts=Contacts,effects=ActiveEffects,rejected=RejectedSpawns,
            spawners=spawners.Values.Where(s=>s.source).Select(s=>new{sourceId=s.source.sourceId,type=s.source.sourceType,enabled=s.source.isActiveAndEnabled,current=s.current,cap=(int)s.source.Number("max_amount_of_NPC",50),interval=s.source.Number("spawm_time_value",2)}).ToArray(),
            actors=actors.Where(a=>a).Select(a=>a.Snapshot()).ToArray()
        };
        public void Stop()
        {
            foreach(var actor in actors.ToArray())if(actor){actor.StopNavigation();if(actor.Spawned)Destroy(actor.gameObject);}
            foreach(var effect in effects)if(effect.gameObject)Destroy(effect.gameObject);effects.Clear();
        }
    }
    /// <summary>Owned clones keep their original serialized proxies without registering
    /// transient instances in the scene-wide narrative and visual component caches.</summary>
    public sealed class OriginalCrowdOwned:MonoBehaviour { }
}
