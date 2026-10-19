using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Sparc.Original
{
    /// <summary>Original waypoint choreography and original camera delays, executed without legacy scripts.</summary>
    public sealed class OriginalSceneMotion:MonoBehaviour
    {
        public OriginalRuntime runtime;
        readonly HashSet<int> routes=new();
        readonly Dictionary<int,HashSet<int>> activatedByRoute=new();
        readonly Dictionary<int,int> cameraRequests=new();
        readonly Dictionary<int,Vector3> positions=new();
        readonly Dictionary<int,float> phases=new();
        readonly Dictionary<int,float> burstEnds=new();
        public int ReachedWaypoints {get;private set;}
        public bool StartRoute(OriginalBehaviour owner)
        {
            if(owner.sourceType!="NPCMove2GivenDestination")return false;
            if(routes.Add(owner.GetInstanceID()))StartCoroutine(Route(owner));return true;
        }
        IEnumerator Route(OriginalBehaviour owner)
        {
            activatedByRoute[owner.GetInstanceID()]=new HashSet<int>();
            try
            {
                var hud=owner.Object("PlayerCanvas");if(hud)runtime.SetActive(hud,false);
                foreach(var value in owner.Data["objs2DisableAfterStartMove"] as JArray??new JArray()){var target=OriginalBehaviour.AsGameObject(owner.Resolve(value));if(target)runtime.SetActive(target,false);}
                StartCoroutine(EnableAfter(owner.Object("object2ActivateAfterStartMove"),owner.Number("DelayForEnableObjectsAfterStartMove",8.88f),owner));
                yield return new WaitForSeconds(Mathf.Max(0,owner.Number("StartMoveDelay",4)));
                if(!owner)yield break;StartCoroutine(ReleasePlayerAfter(owner,owner.Number("DelayForEnablePlayerAction",5.55f)));
                var destinations=owner.Data["destinations"] as JArray??new JArray();var events=owner.Data["destinationReachedEvents"] as JArray;var delays=owner.Data["destinationEventDelays"] as JArray;
                var agent=owner.GetComponent<NavMeshAgent>();var anim=owner.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.runtimeAnimatorController&&a.parameters.Any(p=>p.name=="MoveSpeed"));
                for(int i=0;i<destinations.Count;i++)
                {
                    var destination=OriginalBehaviour.AsGameObject(owner.Resolve(destinations[i]));if(!destination){runtime.Missing("original-waypoint-reference:"+owner.sourceId+":"+i);yield break;}
                    bool navigation=agent&&agent.enabled&&agent.isOnNavMesh;if(navigation){agent.stoppingDistance=0;agent.isStopped=false;agent.SetDestination(destination.transform.position);}
                    Vector3 previous=owner.transform.position;
                    while(owner&&destination)
                    {
                        var difference=destination.transform.position-owner.transform.position;
                        if(difference.magnitude<=owner.Number("destinationReachDistance",1))break;
                        if(!navigation&&owner.Flag("allowDirectMovementFallback",true))
                        {float step=owner.Number("directMovementSpeed",2.5f)*Time.deltaTime;owner.transform.position+=difference.normalized*Mathf.Min(step,difference.magnitude);var horizontal=new Vector3(difference.x,0,difference.z);if(horizontal.sqrMagnitude>0.0001f)owner.transform.rotation=Quaternion.Slerp(owner.transform.rotation,Quaternion.LookRotation(horizontal),1-Mathf.Exp(-8*Time.deltaTime));}
                        if(anim){float speed=Vector3.Distance(previous,owner.transform.position)/Mathf.Max(0.001f,Time.deltaTime);anim.SetFloat("MoveSpeed",speed);previous=owner.transform.position;}
                        yield return null;
                    }
                    ReachedWaypoints++;
                    if(events!=null&&i<events.Count)StartCoroutine(WaypointEvent(owner,events[i],delays!=null&&i<delays.Count?(float)delays[i]:0));
                }
                if(agent&&agent.enabled&&agent.isOnNavMesh)agent.isStopped=true;if(anim)anim.SetFloat("MoveSpeed",0);
                ReleasePlayer(owner);runtime.story.Execute("RBC_CAP_TALK");
            }
            finally{if(owner)routes.Remove(owner.GetInstanceID());}
        }
        void ReleasePlayer(OriginalBehaviour owner)
        {
            if(!owner||!runtime.Started||runtime.ShuttingDown)return;
            var talk=owner.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType=="NPC_Talk");
            if(talk){CameraMode(talk,"DisableNPCVirtualCameraWOMask");CameraMode(talk,"DisableNPCFollowCamera");}
            var canvas=owner.Object("PlayerCanvas");if(canvas)runtime.SetActive(canvas,true);
            foreach(var reference in owner.Data["objs2DisableAfterStartMove"] as JArray??new JArray())
            {var item=OriginalBehaviour.AsGameObject(owner.Resolve(reference));if(item)runtime.SetActive(item,false);}
        }
        IEnumerator ReleasePlayerAfter(OriginalBehaviour owner,float delay)
        {if(delay>0)yield return new WaitForSeconds(delay);ReleasePlayer(owner);}
        IEnumerator EnableAfter(GameObject target,float delay,OriginalBehaviour route=null){if(delay>0)yield return new WaitForSeconds(delay);if(!target||!runtime.Started||runtime.ShuttingDown)yield break;if(route&&!activatedByRoute[route.GetInstanceID()].Add(target.GetInstanceID()))yield break;runtime.SetActive(target,true);}
        IEnumerator WaypointEvent(OriginalBehaviour owner,JToken value,float delay)
        {
            if(delay>0)yield return new WaitForSeconds(delay);if(!owner||!runtime.Started||runtime.ShuttingDown)yield break;
            var preserved=value.DeepClone();if(preserved["m_PersistentCalls"]?["m_Calls"] is JArray calls)
                foreach(var call in calls.ToArray())
                {if((string)call["m_MethodName"]!="SetActive"||(int?)call["m_Arguments"]?["m_BoolArgument"]!=1)continue;var target=OriginalBehaviour.AsGameObject(owner.Resolve(call["m_Target"]));if(target&&target==owner.Object("object2ActivateAfterStartMove")&&!activatedByRoute[owner.GetInstanceID()].Add(target.GetInstanceID()))calls.Remove(call);}
            runtime.EventsValue(owner,preserved);
        }
        public void CameraMode(OriginalBehaviour actor,string method)
        {
            int id=actor.GetInstanceID();int generation=cameraRequests.TryGetValue(id,out var g)?g+1:1;cameraRequests[id]=generation;
            var fixedCamera=actor.Object("NPC_VirtualCamera");var following=actor.Object("NPC_Follow_VirtualCamera");
            if(method=="EnableNPCFollowCamera")
            {if(fixedCamera)runtime.SetActive(fixedCamera,true);StartCoroutine(FollowAfter(actor,id,generation));return;}
            var target=method.Contains("Follow")?following:fixedCamera;if(target)runtime.SetActive(target,method.StartsWith("Enable",StringComparison.Ordinal));
        }
        IEnumerator FollowAfter(OriginalBehaviour actor,int id,int generation)
        {
            yield return new WaitForSeconds(2.33f);if(!actor||cameraRequests[id]!=generation)yield break;
            var fixedCamera=actor.Object("NPC_VirtualCamera");var following=actor.Object("NPC_Follow_VirtualCamera");if(fixedCamera)runtime.SetActive(fixedCamera,false);if(following)runtime.SetActive(following,true);
        }
        void Update()
        {
            if(!runtime||!runtime.Started)return;
            foreach(var component in runtime.components)
            {
                if(!component||!component.isActiveAndEnabled)continue;
                var type=component.sourceType;
                if(type=="NPCMobile")
                {var animator=component.Component<Animator>("Animator");var agent=component.GetComponent<NavMeshAgent>();if(animator&&agent&&agent.enabled&&agent.isOnNavMesh&&!component.GetComponents<OriginalBehaviour>().Any(c=>c.sourceType=="NPCMove2GivenDestination"&&routes.Contains(c.GetInstanceID()))&&animator.runtimeAnimatorController&&animator.parameters.Any(p=>p.name=="MoveSpeed"))animator.SetFloat("MoveSpeed",agent.velocity.magnitude);continue;}
                if(type is not ("GermFloating" or "RBCBurst" or "ObjectRotation"))continue;
                int id=component.GetInstanceID();if(!positions.ContainsKey(id)){positions[id]=component.transform.position;phases[id]=UnityEngine.Random.Range(0f,6.28f);}
                if(type=="RBCBurst")
                {
                    if(!burstEnds.TryGetValue(id,out var end)){end=Time.time+5;burstEnds[id]=end;Destroy(component.gameObject,5);}
                    if(Time.time>=end){positions.Remove(id);phases.Remove(id);burstEnds.Remove(id);Destroy(component.gameObject);continue;}
                }
                if(runtime.visuals.TimelineOwns(component.transform))continue;
                if(type=="GermFloating")
                {var position=component.transform.position;position.y=positions[id].y+Mathf.Sin(Time.time*component.Number("frequency",1)+phases[id])*component.Number("amplitude",0.1f);component.transform.position=position;}
                else if(type=="RBCBurst")
                {
                    component.transform.position+=component.transform.forward*component.Number("moveSpeed",1)*Time.deltaTime;
                    var position=component.transform.position;position.y=positions[id].y+Mathf.Sin(Time.time*component.Number("floatFrequency",1))*component.Number("floatAmplitude",.5f);component.transform.position=position;
                }
                else component.transform.Rotate(OriginalVisuals.Vector(component.Data["rotationSpeed"],new Vector3(0,20,0))*Time.deltaTime,Space.Self);
            }
        }
    }
}
