using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Sparc.Original
{
    /// <summary>Uses the original rigs, materials and authored clips. Procedural limbs are actor-local.</summary>
    public sealed class OriginalVisuals:MonoBehaviour
    {
        public OriginalRuntime runtime;
        sealed class Limb {public Transform transform;public Vector3 position;public Quaternion rotation;public bool leg;public int index;}
        sealed class Rig {public OriginalBehaviour source;public Limb[] limbs;}
        readonly List<Rig> rigs=new();
        readonly Dictionary<int,float> timers=new();
        readonly Dictionary<int,Vector3> restPositions=new();
        readonly Dictionary<int,Quaternion> restRotations=new();
        readonly Dictionary<int,Coroutine> turns=new();
        readonly HashSet<int> configured=new();
        readonly List<OriginalBehaviour> expressions=new();
        readonly List<OriginalBehaviour> talkFaces=new();
        readonly Dictionary<int,PlayableGraph> oneShots=new();
        public int OriginalWaveStarts {get;private set;}
        public int PunchEvents {get;private set;}
        public int MovingLimbSamples {get;private set;}
        public void Initialize(IEnumerable<OriginalBehaviour> components){foreach(var c in components)Activated(c);}
        public void Activated(OriginalBehaviour c)
        {
            if(!c||!configured.Add(c.GetInstanceID()))return;
            restPositions[c.GetInstanceID()]=c.transform.localPosition;restRotations[c.GetInstanceID()]=c.transform.localRotation;
            if(c.sourceType=="RBCLimbAnimation")
            {
                var parts=c.GetComponentsInChildren<Transform>(true).Where(t=>t!=c.transform&&t.CompareTag("limb")).ToArray();
                rigs.Add(new Rig{source=c,limbs=parts.Select((t,i)=>new Limb{transform=t,position=t.localPosition,rotation=t.localRotation,leg=t.name.Contains("LEG",StringComparison.OrdinalIgnoreCase),index=i}).ToArray()});
            }
            if(c.sourceType=="SwitchEmotion")expressions.Add(c);
            if(c.sourceType=="NPC_Talk_MaterialAnimation"){talkFaces.Add(c);var renderer=c.GetComponent<Renderer>();var material=c.Ref("material1") as Material;if(renderer&&material)renderer.sharedMaterial=material;}
        }
        public bool TimelineOwns(Transform t)
        {
            foreach(var guard in runtime.directors)
            {
                if(!guard||!guard.Playing||guard.director.playableAsset is not TimelineAsset timeline)continue;
                foreach(var output in timeline.outputs)
                {
                    var binding=guard.director.GetGenericBinding(output.sourceObject);var go=OriginalBehaviour.AsGameObject(binding);
                    if(go&&(go.transform==t||t.IsChildOf(go.transform)))return true;
                }
            }
            return false;
        }
        void Update()
        {
            if(!runtime||!runtime.Started)return;
            foreach(var rig in rigs)
            {
                var c=rig.source;if(!c||!c.isActiveAndEnabled||TimelineOwns(c.transform))continue;
                foreach(var limb in rig.limbs)
                {
                    if(!limb.transform)continue;float wave=Mathf.Sin(Time.time*c.Number("animationSpeed",6.66f)+limb.index);
                    if(limb.leg){float sign=limb.transform.name.Contains("Right",StringComparison.OrdinalIgnoreCase)?1:-1;limb.transform.localPosition=limb.position+Vector3.forward*wave*c.Number("legMovementRange",0.1f)*sign;limb.transform.localRotation=limb.rotation*Quaternion.Euler(0,0,wave*c.Number("legRotationRange",10));}
                    else{limb.transform.localPosition=limb.position+Vector3.up*wave*c.Number("movementRange",0.1f);var axis=Vector(c.Data["rotationDirection"],Vector3.forward);limb.transform.localRotation=limb.rotation*Quaternion.Euler(axis*wave*c.Number("movementRange",0.1f));}
                    MovingLimbSamples++;
                }
            }
            foreach(var face in talkFaces)
            {
                if(!face||!face.isActiveAndEnabled)continue;var renderer=face.GetComponent<Renderer>();if(!renderer)continue;
                var actor=face.GetComponentsInParent<OriginalBehaviour>(true).FirstOrDefault(b=>b.sourceType=="NPC_Talk");
                bool talking=actor&&runtime.presentation.IsSpeaking&&(string.Equals(actor.name,runtime.presentation.Speaker,StringComparison.OrdinalIgnoreCase)||actor.name.IndexOf(runtime.presentation.Speaker??"<none>",StringComparison.OrdinalIgnoreCase)>=0);
                bool alternate=talking&&(int)(Time.time/Mathf.Max(0.04f,face.Number("switchInterval",0.5f)))%2!=0;
                var material=face.Ref(alternate?"material2":"material1") as Material;if(material&&renderer.sharedMaterial!=material)renderer.sharedMaterial=material;
            }
            foreach(var c in expressions)
            {
                if(!c||!c.isActiveAndEnabled)continue;var marker=c.Object("switcher");if(!marker||!marker.activeSelf)continue;
                float interval=Mathf.Max(0.04f,c.Number("talkInterval",0.6f));int tick=Mathf.FloorToInt(Time.time/interval);var all=c.Data["allMaterials"] as JArray;
                if(all==null||all.Count==0)continue;var material=c.Resolve(all[tick%all.Count]) as Material;if(!material)continue;
                var renderer=c.Component<Renderer>("objectRenderer")??c.GetComponent<Renderer>()??c.GetComponentInChildren<Renderer>();if(renderer&&renderer.sharedMaterial!=material)renderer.sharedMaterial=material;
            }
        }
        public static Vector3 Vector(JToken data,Vector3 fallback=default)=>data is JObject?new Vector3((float?)data["x"]??fallback.x,(float?)data["y"]??fallback.y,(float?)data["z"]??fallback.z):fallback;
        public void White(OriginalBehaviour c,string action)
        {
            var animator=c.Component<Animator>("animator")??c.GetComponent<Animator>();if(!animator)return;
            animator.enabled=true;
            void Bool(string name,bool value){if(animator.parameters.Any(p=>p.name==name&&p.type==AnimatorControllerParameterType.Bool))animator.SetBool(name,value);}
            switch(action)
            {
                case "WBC_Punch":Bool("punch",true);animator.Play("Punching");break;
                case "WBC_Wave":Bool("wave",true);Bool("punch",false);StartCoroutine(PlayOriginalWave(animator));break;
                case "OnPlayerInteract":Bool("punch",false);if(c.Ref("npc_talk") is Behaviour talk)talk.enabled=true;var particles=c.Object("GermParticle");if(particles)particles.SetActive(true);break;
                case "OnPlayerLeave":Bool("punch",true);if(c.Ref("npc_talk") is Behaviour left)left.enabled=false;var germs=c.Object("GermObjects");if(germs)Face(c.transform,germs.transform,5);var hide=c.Object("GermParticle");if(hide)hide.SetActive(false);break;
                case "PlayPunchSound":case "playPunchEffect":
                    PunchEvents++;var effect=c.Component<ParticleSystem>("punchEffect");if(effect){effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);effect.Play();}
                    var audio=c.Component<AudioSource>("audioSource");if(audio&&audio.clip)audio.Play();var germ=c.Component<Animator>("GermBeingHitAnimator");if(germ)germ.Play("GotHit");break;
            }
        }
        IEnumerator PlayOriginalWave(Animator animator)
        {
            if(!runtime.originalWhiteWave){runtime.Missing("original-white-wave-clip");yield break;}
            int id=animator.GetInstanceID();if(oneShots.TryGetValue(id,out var previous)&&previous.IsValid())previous.Destroy();
            // The original WBC controller lacks the Waving state invoked by its source script.
            // Drive the actual matching original wave clip; do not invent a clip or change a Timeline.
            var graph=PlayableGraph.Create("Original White wave");graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output=AnimationPlayableOutput.Create(graph,"Original Wave",animator);var clip=AnimationClipPlayable.Create(graph,runtime.originalWhiteWave);clip.SetApplyFootIK(false);output.SetSourcePlayable(clip);oneShots[id]=graph;OriginalWaveStarts++;graph.Play();
            yield return new WaitForSeconds(5.66f);
            if(oneShots.TryGetValue(id,out var current)&&current.Equals(graph)){if(graph.IsValid())graph.Destroy();oneShots.Remove(id);}
            if(animator){if(animator.parameters.Any(p=>p.name=="wave"))animator.SetBool("wave",false);animator.Play("Idle");}
        }
        void OnDestroy(){foreach(var graph in oneShots.Values)if(graph.IsValid())graph.Destroy();oneShots.Clear();}
        IEnumerator IdleAfter(Animator animator,float wait){yield return new WaitForSeconds(wait);if(animator){animator.Play("Idle");if(animator.parameters.Any(p=>p.name=="wave"))animator.SetBool("wave",false);}}
        public void Face(Transform from,Transform to,float speed)
        {if(!from||!to)return;int id=from.GetInstanceID();if(turns.TryGetValue(id,out var previous)&&previous!=null)StopCoroutine(previous);turns[id]=StartCoroutine(Turn(from,to,Mathf.Max(0.2f,speed)));}
        IEnumerator Turn(Transform from,Transform to,float speed)
        {
            float deadline=Time.time+2;while(from&&to&&Time.time<deadline){var delta=to.position-from.position;delta.y=0;if(delta.sqrMagnitude<0.0001f)yield break;from.rotation=Quaternion.Slerp(from.rotation,Quaternion.LookRotation(delta),1-Mathf.Exp(-speed*Time.deltaTime));yield return null;}
        }
        public void FollowNpc(OriginalBehaviour c,Transform target)
        {
            if(!c||!target)return;var agent=c.GetComponent<NavMeshAgent>();if(!agent)return;
            int id=c.GetInstanceID();if(timers.TryGetValue(id,out float until)&&Time.time<until)return;timers[id]=Time.time+Mathf.Max(0.1f,c.Number("followDestinationUpdateInterval",0.2f));
            if(!agent.enabled)agent.enabled=true;if(agent.isOnNavMesh){agent.isStopped=false;agent.SetDestination(target.position);}
        }
        public bool Teleport(OriginalBehaviour c)
        {
            if(c.sourceType is not ("Teleport" or "ConversationTeleportHandler" or "ConversationPositionTeleport"))return false;
            var player=runtime.player;if(!player)return false;
            if(c.sourceType=="ConversationTeleportHandler")return c.Ref("teleporter") is OriginalBehaviour teleporter&&Teleport(teleporter);
            if(c.sourceType=="ConversationPositionTeleport")
            {
                var targets=c.Data["targets"] as JArray;var locations=c.Data["positionSources"] as JArray;
                if(targets==null||locations==null||targets.Count!=locations.Count){runtime.Missing("conversation-teleport-pairs:"+c.sourceId);return true;}
                for(int i=0;i<targets.Count;i++){var actor=OriginalBehaviour.AsGameObject(c.Resolve(targets[i]));var position=OriginalBehaviour.AsGameObject(c.Resolve(locations[i]));if(actor&&position)Place(actor,position.transform.position,false);}
                return true;
            }
            var destination=c.Object("PlayerTeleportLocation");if(!destination){runtime.Missing("teleport-config:"+c.sourceId);return true;}
            Place(player.gameObject,destination.transform.position,true);
            var red=c.Object("RBC");var cap=c.Object("PeerAgent");var redSpot=c.Object("RBCTeleportLocation");var capSpot=c.Object("PeerAgentTeleportLocation");
            if(red)Place(red,redSpot?redSpot.transform.position:player.transform.position+new Vector3(-1.3f,0,2.1f),true);
            if(cap)Place(cap,capSpot?capSpot.transform.position:player.transform.position+new Vector3(-3,0,-2),true);return true;
        }
        void Place(GameObject actor,Vector3 position,bool restoreAgent)
        {
            var agents=actor.GetComponentsInChildren<NavMeshAgent>(true);var active=agents.Select(a=>a.enabled).ToArray();foreach(var a in agents)a.enabled=false;
            var body=actor.GetComponent<Rigidbody>();if(body){body.position=position;if(!body.isKinematic)body.linearVelocity=Vector3.zero;}actor.transform.position=position;
            if(restoreAgent)for(int i=0;i<agents.Length;i++)if(active[i]&&NavMesh.SamplePosition(position,out var hit,2,NavMesh.AllAreas)){agents[i].enabled=true;agents[i].Warp(hit.position);}
        }
        public bool FollowCamera(OriginalBehaviour c,string method)
        {
            if(c.sourceType!="AssignTargetGroupFollow")return false;
            var camera=c.Component<CinemachineVirtualCamera>("virtualCamera")??c.Component<CinemachineVirtualCamera>("cinemachineCamera");
            var target=c.Object("targetGroup")??c.Object("target");
            if(camera)camera.Follow=method=="DetachFollow"?null:target?.transform;else runtime.Missing("camera-follow-config:"+c.sourceId);return true;
        }
        public bool StartMove(OriginalBehaviour c)
        {
            if(c.sourceType!="NPCMove2GivenDestination")return false;
            var agent=c.GetComponent<NavMeshAgent>()??c.Component<NavMeshAgent>("agent");var target=c.Object("destination")??c.Object("target");
            if(agent&&target&&agent.isOnNavMesh){agent.isStopped=false;agent.SetDestination(target.transform.position);}else runtime.Missing("npc-destination-config:"+c.sourceId);return true;
        }
        public bool Effect(OriginalBehaviour c,string method)
        {
            if(!c.sourceType.Contains("Particle",StringComparison.OrdinalIgnoreCase)&&!c.sourceType.Contains("Effect",StringComparison.OrdinalIgnoreCase))return false;
            string suffix=method.EndsWith("1",StringComparison.Ordinal)?"1":"2";
            var system=c.Component<ParticleSystem>("effectPos"+suffix)??c.Component<ParticleSystem>("particleSystem"+suffix);
            if(system)system.Play();else runtime.Missing("animation-effect-config:"+c.sourceType+":"+method);return true;
        }
    }
}
