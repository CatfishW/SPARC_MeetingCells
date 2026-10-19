#if UNITY_WEBGL && DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Development-only observation. Does not expose commands, teleportation or story mutation.</summary>
    public sealed class OriginalReadOnlyProbe:MonoBehaviour
    {
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern void SPARC_OriginalSnapshot(string json);
        OriginalRuntime runtime;float next;
        readonly Dictionary<int,Quaternion[]> oldBones=new();
        readonly Dictionary<int,int> movedBones=new();
        Animator[] actors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ObserveSceneLoads()
        {
            SceneManager.sceneLoaded-=SceneLoaded;
            SceneManager.sceneLoaded+=SceneLoaded;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()=>Attach(SceneManager.GetActiveScene());
        // sceneLoaded runs after the new runtime's Awake and before the observer's Start.
        // A replay destroys the previous observer; attach to this scene rather than cache it.
        static void SceneLoaded(Scene scene,LoadSceneMode mode)=>Attach(scene);
        static void Attach(Scene scene)
        {
            foreach(var r in FindObjectsByType<OriginalRuntime>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(r.gameObject.scene==scene&&!r.GetComponent<OriginalReadOnlyProbe>())r.gameObject.AddComponent<OriginalReadOnlyProbe>();
        }
        void Start(){runtime=GetComponent<OriginalRuntime>();actors=FindObjectsByType<Animator>(FindObjectsInactive.Include,FindObjectsSortMode.None);}
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        object Bounds(RectTransform rect)
        {
            var canvas=rect.GetComponentInParent<Canvas>();Camera camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var p=corners.Select(c=>RectTransformUtility.WorldToScreenPoint(camera,c)).ToArray();
            float x=p.Min(v=>v.x),y=p.Min(v=>v.y),w=p.Max(v=>v.x)-x,h=p.Max(v=>v.y)-y;
            return new{x=x/Screen.width,y=(Screen.height-y-h)/Screen.height,w=w/Screen.width,h=h/Screen.height};
        }
        static object ViewportRect(Rect r)=>new{x=r.x,y=r.y,w=r.width,h=r.height};
        Vector2 ScreenPosition(Transform target)
        {
            var canvas=target.GetComponentInParent<Canvas>();var camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var p=RectTransformUtility.WorldToScreenPoint(camera,target.position);return new Vector2(p.x/Screen.width,1-p.y/Screen.height);
        }
        object MiniPlayer(GameObject player)
        {if(!player)return null;var p=ScreenPosition(player.transform);return new{x=p.x,y=p.y};}
        object MiniJoystick(OriginalMiniGame game)
        {
            var stick=game.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.InputSystem.OnScreen.OnScreenStick>(true).FirstOrDefault(s=>s.isActiveAndEnabled);
            return stick?Bounds((RectTransform)stick.transform):null;
        }
        object MiniInput()
        {
            var pad=UnityEngine.InputSystem.Gamepad.current;if(pad==null)return null;var value=pad.leftStick.ReadValue();return new{x=value.x,y=value.y};
        }
        object MiniBoard(OriginalMiniGame game)
        {
            var objects=game.Area.GetComponentsInChildren<Collider2D>().Where(c=>c.enabled&&!c.isTrigger&&!c.GetComponentInParent<OriginalMiniToken>()&&!c.GetComponentInParent<OriginalMiniPlayer>());
            var bounds=objects.Select(c=>{var r=c.bounds;var low=RectTransformUtility.WorldToScreenPoint(null,r.min);var high=RectTransformUtility.WorldToScreenPoint(null,r.max);return new{x=low.x/Screen.width,y=1-high.y/Screen.height,w=(high.x-low.x)/Screen.width,h=(high.y-low.y)/Screen.height,circle=c is CircleCollider2D};}).ToArray();
            var player=game.Player.GetComponent<Collider2D>();var radius=player?player.bounds.extents:Vector3.one;
            return new{rect=Bounds(game.Area),obstacles=bounds,playerRadius=new{x=radius.x/Screen.width,y=radius.y/Screen.height},speed=game.Player.GetComponent<OriginalMiniPlayer>().source.Number("_speed",350)*game.Player.transform.parent.lossyScale.x};
        }
        string ButtonText(Button button)
        {var text=button.GetComponentInChildren<TMP_Text>();if(text)return text.text;var legacy=button.GetComponentInChildren<Text>();return legacy?legacy.text:"";}
        object Pose(Animator actor)
        {
            var bones=actor.GetComponentsInChildren<Transform>();var current=bones.Select(b=>b.localRotation).ToArray();int id=actor.GetInstanceID();int moved=0;
            if(oldBones.TryGetValue(id,out var old)&&old.Length==current.Length)moved=current.Where((r,i)=>Quaternion.Angle(old[i],r)>0.1f).Count();
            oldBones[id]=current;movedBones.TryAdd(id,0);movedBones[id]+=moved;
            var state=actor.GetCurrentAnimatorStateInfo(0);
            return new{name=actor.name,path=PathOf(actor.transform),controller=actor.runtimeAnimatorController.name,animationSpeed=actor.speed,boundPlayables=actor.hasBoundPlayables,parameters=actor.parameters.Where(p=>p.type is AnimatorControllerParameterType.Float or AnimatorControllerParameterType.Bool).Select(p=>new{name=p.name,value=p.type==AnimatorControllerParameterType.Float?actor.GetFloat(p.name):(actor.GetBool(p.name)?1f:0f)}).ToArray(),clips=actor.GetCurrentAnimatorClipInfo(0).Select(c=>new{name=c.clip.name,weight=c.weight}).ToArray(),stateTime=state.normalizedTime,movedBones=moved,accumulatedMovedBones=movedBones[id]};
        }
        bool Visible(Behaviour item)
        {
            if(!item||!item.isActiveAndEnabled)return false;var canvas=item.GetComponentInParent<Canvas>();if(!canvas||!canvas.isActiveAndEnabled)return false;
            foreach(var group in item.GetComponentsInParent<CanvasGroup>())if(group.alpha<0.01f||!group.interactable)return false;
            return item is not Selectable select||select.IsInteractable();
        }
        string[] PointerTargets()
        {
            var system=UnityEngine.EventSystems.EventSystem.current;var mouse=UnityEngine.InputSystem.Mouse.current;if(!system||mouse==null)return Array.Empty<string>();
            var pointer=new UnityEngine.EventSystems.PointerEventData(system){position=mouse.position.ReadValue()};var hits=new List<UnityEngine.EventSystems.RaycastResult>();system.RaycastAll(pointer,hits);return hits.Take(5).Select(hit=>PathOf(hit.gameObject.transform)).ToArray();
        }
        void LateUpdate()
        {
            if(!runtime||Time.unscaledTime<next)return;next=Time.unscaledTime+0.2f;
            var player=runtime.player;var presentation=runtime.presentation;var talk=runtime.objectives.ActiveTalk?.Object("NPCTalkTo");var talkPosition=talk?talk.transform.position:Vector3.zero;
            var snapshot=new{frame=Time.frameCount,started=runtime.Started,block=runtime.story.CurrentBlock,lineId=presentation.LineId,line=presentation.Line,speaker=presentation.Speaker,speaking=presentation.IsSpeaking,shownCharacters=presentation.VisibleCharacters,
                player=player?new{x=player.transform.position.x,y=player.transform.position.y,z=player.transform.position.z,active=player.gameObject.activeInHierarchy,modelActive=player.model&&player.model.activeInHierarchy,thirdPerson=player.ThirdPerson,cleared=player.ClearedRenderers,driverAnimatorBound=player.animator!=null,driverAnimatorPath=player.animator?PathOf(player.animator.transform):null,yaw=player.transform.eulerAngles.y,distance=player.DistanceTravelled,walking=player.Walking,canControl=player.CanControl,touchMove=new{x=player.TouchMove.x,y=player.TouchMove.y},crowdPush=new{x=player.CrowdVelocity.x,z=player.CrowdVelocity.z}}:null,
                timelines=runtime.directors.Select(d=>new{name=d.name,active=d.gameObject.activeInHierarchy,asset=d.director.playableAsset?d.director.playableAsset.name:null,time=d.director.time,starts=d.Starts,playing=d.Playing,duration=d.director.duration}).ToArray(),
                cameras=FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c=>c.isActiveAndEnabled).Select(c=>new{name=c.name,position=new{x=c.transform.position.x,y=c.transform.position.y,z=c.transform.position.z},depth=c.depth,mask=c.cullingMask}).ToArray(),
                actors=actors.Where(a=>a&&a.isActiveAndEnabled&&a.runtimeAnimatorController).Select(Pose).ToArray(),
                buttons=FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>Visible(b)).Select(b=>new{name=b.name,path=PathOf(b.transform),text=ButtonText(b),rect=Bounds((RectTransform)b.transform)}).ToArray(),
                launchers=FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(b=>b.name is "PlayGameRBC" or "PlayGameWBC").Select(b=>new{name=b.name,active=b.gameObject.activeInHierarchy,self=b.gameObject.activeSelf,rect=Bounds((RectTransform)b.transform),ancestors=b.GetComponentsInParent<RectTransform>(true).Select(r=>new{name=r.name,w=r.rect.width,h=r.rect.height,x=r.position.x,y=r.position.y,scale=r.lossyScale.x}).ToArray()}).ToArray(),
                npcs=runtime.OfType("NPC_Talk").Select(n=>new{name=n.name,active=n.isActiveAndEnabled,position=new{x=n.transform.position.x,y=n.transform.position.y,z=n.transform.position.z}}).ToArray(),
                reachObjectives=runtime.OfType("ObjectiveReachPoint").Where(o=>o.isActiveAndEnabled).Select(o=>new{name=o.name,title=o.Text("Title"),description=o.Text("Description"),targets=o.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&c.isTrigger).Select(c=>new{x=c.bounds.center.x,y=c.bounds.center.y,z=c.bounds.center.z,radius=Mathf.Min(c.bounds.extents.x,c.bounds.extents.z)}).ToArray()}).ToArray(),
                talkObjective=talk?new{name=talk.name,position=new{x=talkPosition.x,y=talkPosition.y,z=talkPosition.z}}:null,
                nearestTalk=runtime.NearestTalk?runtime.NearestTalk.name:null,
                interaction=runtime.interaction?runtime.interaction.Snapshot:null,
                handoff=new{started=runtime.HandoffStarted,completed=runtime.HandoffCompleted,playing=runtime.HandoffPlaying,phase=runtime.HandoffPhase,peakTime=runtime.HandoffPeakTime,elapsed=runtime.HandoffElapsed,duration=runtime.HandoffDuration,starts=runtime.HandoffStarts,cardVisible=presentation.CompletionVisible,cardRect=ViewportRect(presentation.CompletionCardViewportRect),replayRect=ViewportRect(presentation.CompletionReplayViewportRect)},
                waypoints=runtime.motion.ReachedWaypoints,objective=runtime.objectives.ActiveTalk?runtime.objectives.ActiveTalk.Text("Title"):null,completedObjectives=runtime.objectives.CompletedCount,
                games=runtime.activities.Games.Where(g=>g&&g.source.isActiveAndEnabled&&!g.Continued).Select(g=>new{white=g.IsWhite,g.Score,g.Level,g.Elapsed,g.Duration,g.Finished,g.Transitioning,streak=g.Visuals?g.Visuals.Streak:0,bestStreak=g.Visuals?g.Visuals.BestStreak:0,helpExpanded=g.Visuals&&g.Visuals.InstructionsExpanded,remaining=g.RemainingHostiles,miniPlayer=MiniPlayer(g.Player),joystick=MiniJoystick(g),input=MiniInput(),board=MiniBoard(g),tokens=FindObjectsByType<OriginalMiniToken>(FindObjectsSortMode.None).Where(t=>t.game==g).Select(t=>new{id=t.GetInstanceID(),x=ScreenPosition(t.transform).x,y=ScreenPosition(t.transform).y,hostile=t.Hostile,attached=t.Attached}).ToArray(),deposits=g.GetComponentInParent<Canvas>().GetComponentsInChildren<Collider2D>().Where(c=>c.CompareTag("OxygenDeposit")).Select(c=>new{x=ScreenPosition(c.transform).x,y=ScreenPosition(c.transform).y}).ToArray()}).ToArray(),
                mobileWorksheet=GetComponent<OriginalMobileWorksheet>()?.Snapshot,
                wordTiles=FindObjectsByType<OriginalWordTile>(FindObjectsSortMode.None).Where(t=>t.isActiveAndEnabled).Select(t=>new{name=t.name,word=t.Word,rect=Bounds((RectTransform)t.transform)}).ToArray(),
                wordSlots=FindObjectsByType<OriginalWordSlot>(FindObjectsSortMode.None).Where(s=>s.isActiveAndEnabled).Select(s=>new{name=s.name,value=s.Current?s.Current.Word:null,rect=Bounds((RectTransform)s.transform)}).ToArray(),
                fields=FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).Where(f=>f.isActiveAndEnabled&&f.interactable).Select(f=>new{name=f.name,text=f.text,rect=Bounds((RectTransform)f.transform)}).ToArray(),
                touchPads=FindObjectsByType<OriginalTouchPad>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==gameObject.scene).Select(p=>new{name=p.name,path=PathOf(p.transform),active=p.isActiveAndEnabled,enabled=p.Enabled,canvasScale=p.GetComponentInParent<Canvas>()?.scaleFactor??1,look=p.Look,held=p.Held,pointer=p.HeldPointer,value=new{x=p.Value.x,y=p.Value.y},rect=Bounds((RectTransform)p.transform)}).ToArray(),
                crowd=runtime.crowd?runtime.crowd.Snapshot():null,
                pointerTargets=PointerTargets(),
                peerAgent=new{runtime.peerAgent.Pending,runtime.peerAgent.Outcome,runtime.peerAgent.ServiceState,runtime.peerAgent.Answers,runtime.peerAgent.Failures},
                boundaries=runtime.Unhandled.ToArray(),events=runtime.EventTrace.TakeLast(40).ToArray(),trace=runtime.story.Trace.TakeLast(40).ToArray(),completed=runtime.story.CompletedBlocks.ToArray(),punchEvents=runtime.visuals.PunchEvents,limbSamples=runtime.visuals.MovingLimbSamples};
            SPARC_OriginalSnapshot(JsonConvert.SerializeObject(snapshot));
        }
    }
}
#endif
