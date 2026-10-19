using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Sparc.Original
{
    [DefaultExecutionOrder(-32000)] public sealed class OriginalRuntime:MonoBehaviour
    {
        public static OriginalRuntime Instance {get;private set;}
        public OriginalStory story;
        public OriginalPresentation presentation;
        public OriginalPlayer player;
        public OriginalVisuals visuals;
        public OriginalActivities activities;
        public AnimationClip originalWhiteWave;
        public OriginalSceneMotion motion;
        public OriginalCrowd crowd;
        public OriginalObjectives objectives;
        public OriginalHudLayout hud;
        public OriginalActivityUi activityUi;
        public OriginalInteraction interaction;
        public OriginalPeerAgent peerAgent;
        public OriginalBehaviour[] components=Array.Empty<OriginalBehaviour>();
        public OriginalDirector[] directors=Array.Empty<OriginalDirector>();
        public bool Started {get;private set;}
        public bool ShuttingDown {get;private set;}
        public readonly HashSet<string> Unhandled=new();
        public readonly List<string> EventTrace=new();
        OriginalBehaviour nearestTalk;
        public OriginalBehaviour NearestTalk {get=>HandoffStarted?null:nearestTalk;private set=>nearestTalk=value;}
        public bool CinematicActive=>directors.Any(d=>d&&d.Playing);
        const string HeartTransitionName="Storyline_MeetingCellsTransition2Heart";
        OriginalDirector heartTransition;bool handoffLocked,handoffCompleted;float handoffPeak,handoffElapsed;
        public bool HandoffStarted=>handoffLocked||(heartTransition&&heartTransition.Starts>0);
        public bool HandoffCompleted=>handoffCompleted;
        public bool HandoffPlaying=>heartTransition&&heartTransition.Playing;
        public float HandoffPeakTime=>handoffPeak;
        public float HandoffElapsed=>handoffElapsed;
        public double HandoffDuration=>heartTransition&&heartTransition.director?heartTransition.director.duration:0;
        public int HandoffStarts=>heartTransition?heartTransition.Starts:0;
        public string HandoffPhase=>!HandoffStarted?"none":HandoffCompleted?"complete":HandoffPlaying?"playing":"interrupted";
        readonly HashSet<int> initialized=new();
        readonly HashSet<int> delayedActivation=new();
        readonly Dictionary<int,string> talkBlocks=new();
        readonly HashSet<int> forbiddenTalk=new();
        readonly Dictionary<int,bool> allowedFollow=new();
        readonly HashSet<string> effects=new();
        public IEnumerable<OriginalBehaviour> OfType(string type)=>components.Where(c=>c&&c.sourceType==type);
        public OriginalBehaviour First(string type)=>OfType(type).FirstOrDefault();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ConfigureNullGraphicsTests(){if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)QualitySettings.realtimeReflectionProbes=false;}
        void Awake()
        {
            // Unity 6000.3 beta crashes in real-time cubemap shadow rendering on its Null
            // graphics backend. Disable probes only in no-graphics test processes, not in the game.
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                foreach(var probe in FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include,FindObjectsSortMode.None))probe.enabled=false;

            Instance=this;components=FindObjectsByType<OriginalBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.gameObject.scene==gameObject.scene).ToArray();
            directors=FindObjectsByType<OriginalDirector>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.gameObject.scene==gameObject.scene).ToArray();
            heartTransition=directors.FirstOrDefault(d=>d.name==HeartTransitionName);
            story=gameObject.AddComponent<OriginalStory>();story.runtime=this;story.Initialize(components);
            presentation=gameObject.AddComponent<OriginalPresentation>();presentation.runtime=this;
            visuals=gameObject.AddComponent<OriginalVisuals>();visuals.runtime=this;
            activities=gameObject.AddComponent<OriginalActivities>();activities.runtime=this;
            motion=gameObject.AddComponent<OriginalSceneMotion>();motion.runtime=this;
            crowd=gameObject.AddComponent<OriginalCrowd>();crowd.runtime=this;crowd.Initialize();
            objectives=gameObject.AddComponent<OriginalObjectives>();objectives.runtime=this;
            peerAgent=gameObject.AddComponent<OriginalPeerAgent>();peerAgent.runtime=this;
            var source=First("MeetingCellsPlayerMoveController");if(source){player=source.gameObject.GetComponent<OriginalPlayer>()??source.gameObject.AddComponent<OriginalPlayer>();player.runtime=this;player.Initialize(source);}
            foreach(var c in OfType("NPC_Talk")){talkBlocks[c.GetInstanceID()]=c.Text("npcTalk_blockName");if(!c.Flag("Allowed2Talk2Player",true))forbiddenTalk.Add(c.GetInstanceID());allowedFollow[c.GetInstanceID()]=c.Flag("Allowed2FollowPlayer");}
            hud=gameObject.AddComponent<OriginalHudLayout>();hud.runtime=this;
            activityUi=gameObject.AddComponent<OriginalActivityUi>();activityUi.runtime=this;activityUi.Initialize();
            interaction=gameObject.AddComponent<OriginalInteraction>();interaction.runtime=this;
            gameObject.AddComponent<OriginalLighting>();
            gameObject.AddComponent<OriginalUiTheme>();
            var menus=gameObject.AddComponent<OriginalMenuUi>();menus.runtime=this;
            var mobile=gameObject.AddComponent<OriginalMobileControls>();mobile.runtime=this;
            foreach(var touch in OfType("SimpleTouchController")){var pad=OriginalComponents.Ensure<OriginalTouchPad>(touch.gameObject);pad.player=player;pad.Look=touch.name.Contains("Right",StringComparison.OrdinalIgnoreCase);}
        }
        public void Begin()
        {
            if(Started)return;Started=true;visuals.Initialize(components);
            foreach(var c in components.Where(c=>c&&c.isActiveAndEnabled).ToArray())Activated(c);
            foreach(var b in story.Blocks)if(b.Ref("eventHandler") is OriginalBehaviour handler&&handler.sourceType=="GameStarted")story.Execute(b.Text("blockName"));
            foreach(var d in directors)d.Activate();
        }
        public void Register(OriginalBehaviour component)
        {if(component&&!components.Contains(component))components=components.Append(component).ToArray();}
        public void Activated(OriginalBehaviour component)
        {
            if(!Started||ShuttingDown||!component)return;
            if(crowd&&crowd.IsOwned(component)){crowd.Activated(component);return;}
            Register(component);
            switch(component.sourceType)
            {
                case "NPC_spawn":case "RBC_SPAWN_MeetingCells":case "NPC_random_walk":case "NPC_collision_with_player":crowd.Activated(component);break;
                case "ObjectiveTalkWithNPC":case "ObjectiveReachPoint":case "ObjectiveAnswerQuestions":objectives.Activated(component);break;
                case "OnEnableEvent":Events(component,"Event");break;
                case "FungusBlockExecute":if(component.Flag("ShouldExecuteOnEnable"))story.Execute(component.Text("blockName"));break;
                case "ExecuteChartBlockOFpeer":story.Execute(component.Text("chartBlockName"));break;
                case "SubtitleOnEnableTypeWriter":StartCoroutine(TypeOriginalCaption(component));break;
                case "PlayerController2D":case "OxygenController":case "BacteriaController":case "MatchColliderSizeToUI":case "RedBloodCellGameManager":case "ExitConfirmation":activities.Activated(component);break;
                case "EnableDelay":if(delayedActivation.Add(component.GetInstanceID()))StartCoroutine(EnableLater(component));break;
                case "OnVideoEnd":BindVideo(component);break;
                case "WBCAnimation":visuals.White(component,"WBC_Punch");break;
                case "WhiteBloodCellGameManager":case "WordGameManager":case "OnSubmitSceneTransition":activities.Activated(component);break;
                case "SayDialog":if(activityUi)activityUi.HideLegacyDialogueGraphics(component);break;
            }
            if(initialized.Add(component.GetInstanceID()))visuals.Activated(component);
        }
        IEnumerator TypeOriginalCaption(OriginalBehaviour component)
        {
            var label=component.Component<TMP_Text>("subtitleText");if(!label)yield break;
            label.ForceMeshUpdate(true);int length=label.textInfo.characterCount;float speed=Mathf.Max(0.001f,component.Number("typewriterSpeed",0.05f));float elapsed=0;label.maxVisibleCharacters=0;
            while(component&&component.isActiveAndEnabled&&label&&elapsed<length*speed){elapsed+=Time.deltaTime;label.maxVisibleCharacters=Mathf.Min(length,(int)(elapsed/speed));yield return null;}
            if(label)label.maxVisibleCharacters=int.MaxValue;
        }
        public void Deactivated(OriginalBehaviour component)
        {if(Started&&!ShuttingDown&&component&&component.sourceType=="OnEnableEvent")Events(component,"OnDisableEvent");}
        public void Destroyed(OriginalBehaviour component)
        {
            if(!Started||ShuttingDown||!component)return;
            if(component.sourceType=="FungusBlockExecute"&&component.Flag("ShouleExecuteOnDestroy"))story.Execute(component.Text("blockName"));
            if(component.sourceType=="OnDestroyHandler")foreach(var property in component.Data.Properties().Where(p=>p.Value is JObject o&&o["m_PersistentCalls"]!=null))Events(component,property.Name);
        }
        IEnumerator EnableLater(OriginalBehaviour c){var layout=c.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();if(layout)layout.enabled=false;c.gameObject.SetActive(false);yield return new WaitForSeconds(Mathf.Max(0,c.Number("delaySeconds",1.23f)));if(c&&Started&&!ShuttingDown){c.gameObject.SetActive(true);if(layout)layout.enabled=true;}}
        void BindVideo(OriginalBehaviour c)
        {
            if(!initialized.Add(-c.GetInstanceID()))return;
            var video=c.Component<VideoPlayer>("vp");if(!video)return;
            video.loopPointReached+=_=>{if(!c||ShuttingDown)return;var current=c.Object("videoCanvas");var next=c.Object("NextVideoCanvas");if(current)SetActive(current,false);if(next)SetActive(next,true);};
            if(video.isActiveAndEnabled)video.Play();
        }
        public void SetActive(GameObject target,bool active)
        {if(!target)return;EventTrace.Add("active:"+target.name+":"+active);if(target.activeSelf!=active)target.SetActive(active);}
        public void Trigger(OriginalBehaviour c,Collider other,bool enter)
        {
            if(!Started||ShuttingDown||HandoffStarted||!other||!player||!(other.CompareTag("Player")||other.transform.IsChildOf(player.transform)))return;
            if(c.sourceType=="OnTriggerEnterEvent")Events(c,enter?"OnTriggerEnterEventAction":"OnTriggerExitEventAction");
            if(c.sourceType=="DeadEndFlowchartBlockActivate"&&enter)story.Execute(c.Text("blockName","DeadEnd"));
            if(c.sourceType=="WBCAnimation")visuals.White(c,enter?"OnPlayerInteract":"OnPlayerLeave");
            if(c.sourceType=="Teleport"&&enter)visuals.Teleport(c);
            if(c.sourceType=="ObjectiveReachPoint"&&enter)objectives.Reached(c);
        }
        public void Events(OriginalBehaviour owner,string field)
        {
            EventsValue(owner,owner.Data[field]);
        }
        public void EventsValue(OriginalBehaviour owner,JToken value)
        {
            if(value?["m_PersistentCalls"]?["m_Calls"] is not JArray calls)return;
            foreach(var entry in calls)
            {
                if((int?)entry["m_CallState"]==0)continue;
                var target=owner.Resolve(entry["m_Target"]);string method=(string)entry["m_MethodName"]??"";var arg=entry["m_Arguments"];int mode=(int?)entry["m_Mode"]??1;
                object argument=mode switch{2=>owner.Resolve(arg?["m_ObjectArgument"]),3=>(int?)arg?["m_IntArgument"]??0,4=>(float?)arg?["m_FloatArgument"]??0,5=>(string)arg?["m_StringArgument"]??"",6=>((int?)arg?["m_BoolArgument"]??0)!=0,_=>null};
                DispatchTarget(target,method,argument);
            }
        }
        public void DispatchTarget(UnityEngine.Object target,string method,object arg)
        {
            if(!target){Missing("event-target:"+method);return;}
            EventTrace.Add(target.name+"."+method);
            if(target is OriginalBehaviour b){Call(b,method,arg);return;}
            if(method=="SetActive"&&target is GameObject go){SetActive(go,Convert.ToBoolean(arg));return;}
            if(method=="set_enabled"&&target is Behaviour enabled){enabled.enabled=Convert.ToBoolean(arg);return;}
            if(target is PlayableDirector director&&method=="Play"){if(director.state!=PlayState.Playing)director.Play();return;}
            if(target is Animator animator&&method=="Play"){animator.Play(arg as string);return;}
            if(target is GameObject gameObject){CallObject(gameObject,method,arg);return;}
            Missing("native-method:"+target.GetType().Name+"."+method);
        }
        public void CallObject(GameObject target,string method,object arg=null,float delay=0)
        {if(!target){Missing("method-target:"+method);return;}if(delay>0){StartCoroutine(Delayed(target,method,arg,delay));return;}bool handled=false;foreach(var c in target.GetComponents<OriginalBehaviour>())handled|=Call(c,method,arg,false);if(!handled)Missing("method:"+target.name+"."+method);}
        IEnumerator Delayed(GameObject target,string method,object arg,float delay){yield return new WaitForSeconds(delay);if(target&&!ShuttingDown)CallObject(target,method,arg);}
        public bool Call(OriginalBehaviour c,string method,object arg=null,bool report=true)
        {
            if(!c||ShuttingDown)return false;
            bool ok=true;
            switch(method)
            {
                case "ExecuteBlock":if(c.sourceType is "Flowchart" or "FungusBlockExecute")story.Execute(arg as string??c.Text("blockName"));else ok=false;break;
                case "StartTalk":if(c.sourceType=="NPC_Talk"){if(!HandoffStarted){interaction?.OnInteraction(c);StartCoroutine(TalkAfterSignal(c));}}else ok=false;break;
                case "StartObserve":if(c.sourceType=="NPC_Talk"){if(!HandoffStarted){interaction?.OnInteraction(c,true);story.Execute(c.Text("ObserveBlockName"));}}else ok=false;break;
                case "ChangeExecuteBlock":if(c.sourceType=="NPC_Talk"){talkBlocks[c.GetInstanceID()]=arg as string??"";interaction?.ResetTalk(c);}else ok=false;break;
                case "Allow2Talk2Player":if(c.sourceType=="NPC_Talk"){forbiddenTalk.Remove(c.GetInstanceID());interaction?.ResetTalk(c);}else ok=false;break;
                case "DontAllow2Talk2Player":if(c.sourceType=="NPC_Talk")forbiddenTalk.Add(c.GetInstanceID());else ok=false;break;
                case "EnableNPCVirtualCamera":case "EnableNPCFollowCamera":case "DisableNPCFollowCamera":case "DisableNPCVirtualCamera":case "DisableNPCVirtualCameraWOMask":
                    if(c.sourceType=="NPC_Talk")motion.CameraMode(c,method);else ok=false;break;
                case "DisablePlayerMove":case "DisablePlayerInput":if(c.sourceType=="NPC_Talk"&&player)player.InputLocked=true;else ok=false;break;
                case "EnablePlayerMove":case "EnablePlayerInput":if(c.sourceType=="NPC_Talk"&&player)player.InputLocked=false;else ok=false;break;
                case "enable_player_movement":if(c.sourceType=="NPC_interact"&&player)player.InputLocked=false;else ok=false;break;
                case "BeginDrag":case "EndDrag":case "DisableTouchPresence":case "EnableTouchPresence":
                    if(c.sourceType=="SimpleTouchController"){var pad=c.GetComponent<OriginalTouchPad>();if(pad){if(method=="EnableTouchPresence")pad.Enabled=true;if(method=="EndDrag"||method=="DisableTouchPresence")pad.Presence(false);}}else ok=false;break;
                case "NPCFollowPlayer":case "EnableNPCFollow":case "DisableNPCFollow":case "NPCStopFollowPlayer":
                    if(c.sourceType is "NPC_Talk" or "NPC_follow_player")allowedFollow[c.GetInstanceID()]=method is "NPCFollowPlayer" or "EnableNPCFollow";
                    else if(c.sourceType=="Teleport"){foreach(var key in new[]{"RBC","PeerAgent"}){var actor=c.Object(key);if(actor)foreach(var talk in actor.GetComponents<OriginalBehaviour>().Where(t=>t.sourceType=="NPC_Talk"))allowedFollow[talk.GetInstanceID()]=method=="EnableNPCFollow";}}
                    else ok=false;break;
                case "AddPlayerModelToCullingMask":case "RemovePlayerModelFromCullingMask":if(c.sourceType=="NPCLookAtManager"){if(player)player.ShowModel(method.StartsWith("Add",StringComparison.Ordinal));}else ok=false;break;
                case "EnableThirdPersonConversationCamera":case "DisableThirdPersonConversationCamera":if(c.sourceType=="NPCLookAtManager"){var camera=c.Object("ConversationCamera");if(camera)SetActive(camera,method.StartsWith("Enable",StringComparison.Ordinal));}else ok=false;break;
                case "PlayerLookAtRBC":case "PlayerLookAtCAP":case "PlayerLookAtWBC":case "RBCLookAtPlayer":case "RBCLookAtCAP":case "RBCLookAtWBC":case "CAPLookAtPlayer":case "CAPLookAtRBC":case "CAPLookAtWBC":case "WBCLookAtCAP":case "WBCLookAtRBC":
                    if(c.sourceType=="NPCLookAtManager")Face(c,method);else ok=false;break;
                case "WBC_Punch":case "WBC_Wave":case "PlayPunchSound":case "playPunchEffect":case "OnPlayerLeave":if(c.sourceType=="WBCAnimation")visuals.White(c,method);else ok=false;break;
                case "PlayEffect_Pos1":case "PlayEffect_Pos2":ok=visuals.Effect(c,method);break;
                case "TeleportPlayer":case "TeleportTargets":ok=visuals.Teleport(c);break;
                case "AssignFollow":case "DetachFollow":ok=visuals.FollowCamera(c,method);break;
                case "StartMove":ok=motion.StartRoute(c);break;
                case "OnInteractionEnd":case "OnEitherButtonClicked":case "SuppressInteractionUi":case "ReleaseInteractionUi":case "EnableDrawing":case "DisableDrawing":ok=interaction&&interaction.Call(c,method,arg);break;
                case "UpdateAndSendUserInput":case "UpdateUserInput":case "SendClicked":ok=peerAgent.Handles(c,method);break;
                case "SpeakDialogueList":
                    if(c.sourceType=="Text2Speech"&&arg==null)EventTrace.Add("source-invalid-call:SpeakDialogueList-requires-int;original-Say-voice-retained");else ok=false;break;
                case "DoJump":if(player)player.Jump();break;
                case "NextPage":case "PrevPage":case "Submit":case "CompleteQuestion":case "ContinueAfterWin":case "ContinueAfterLoss":case "OnStartButtonClick_ToHeart":case "OnExitButton":case "OnConfirmButton":case "OnCancelButton":case "OnReturnButtonClicked":ok=activities.Call(c,method,arg);break;
                case "set_enabled":c.enabled=Convert.ToBoolean(arg);break;
                case "set_ContinuousRightController":if(player)player.ContinuousLook=Convert.ToBoolean(arg);break;
                case "OnMove":if(player&&arg is Vector2 v)player.TouchMove=HandoffStarted?Vector2.zero:v;break;
                default:ok=false;break;
            }
            if(!ok&&report)Missing("unimplemented:"+c.sourceType+"."+method);
            return ok;
        }
        IEnumerator TalkAfterSignal(OriginalBehaviour speaker)
        {
            objectives.Talked(speaker);
            // Original StartTalk is also a Timeline signal receiver. Permission/proximity gates
            // apply to user selection, not the authored signal that starts CAP's introduction.
            yield return new WaitForSeconds(0.25f);
            if(speaker&&Started&&!ShuttingDown&&!HandoffStarted)story.Execute(talkBlocks.TryGetValue(speaker.GetInstanceID(),out var block)?block:speaker.Text("npcTalk_blockName"));
        }
        bool TalkingOrCinematic()=>presentation.IsSpeaking||CinematicActive;
        void Face(OriginalBehaviour manager,string method)
        {
            string[] pair=method.Split(new[]{"LookAt"},StringSplitOptions.None);if(pair.Length!=2)return;
            GameObject from=pair[0]=="Player"?player?.gameObject:manager.Object(pair[0]);GameObject to=pair[1]=="Player"?player?.gameObject:manager.Object(pair[1]);
            if(!from||!to)return;visuals.Face(from.transform,to.transform,manager.Number("rotationSpeed",5));
            var group=manager.Component<CinemachineTargetGroup>("ConversationGroup");if(group&&group.m_Targets.Length>=2){group.m_Targets[0].target=from.transform;group.m_Targets[1].target=to.transform;}
        }
        public void InvokeSerialized(OriginalBehaviour command)
        {
            // InvokeMethod metadata is retained and requires explicit resolution, never arbitrary reflection/network calls.
            var target=command.Object("targetObject");var method=command.Text("targetMethod");
            // Original source explicitly ignores InvokeMethod records with no configured method.
            if(string.IsNullOrEmpty(method)){EventTrace.Add("source-unconfigured-invocation:"+command.sourceId);return;}
            var receiver=target?target.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType==command.Text("targetComponentText")):null;
            if(receiver)Call(receiver,method);else Missing("InvokeMethod-receiver:"+command.sourceId+":"+method);
        }
        /// <summary>True when the story currently allows talking to this guide (it may be out of reach).</summary>
        public bool TalkAvailable(OriginalBehaviour npc)=>npc&&npc.isActiveAndEnabled&&!forbiddenTalk.Contains(npc.GetInstanceID());
        public void Missing(string boundary)
        {if(Unhandled.Add(boundary))Debug.LogWarning("[OriginalScene boundary] "+boundary);}
        void Update()
        {
            ObserveHandoff(true);
            if(!Started||!player)return;
            if(HandoffStarted){NearestTalk=null;return;}
            var menu=GetComponent<OriginalMenuUi>();if((menu&&menu.IsOpen)||activities.IsOpen||activities.IsWorksheetOpen||(activityUi&&activityUi.IsTaskPanelOpen)){NearestTalk=null;return;}
            // Small hysteresis: crowd pushes at the range edge otherwise blink the Talk prompt.
            var previous=nearestTalk;NearestTalk=null;float closest=100;
            foreach(var npc in OfType("NPC_Talk"))
            {
                if(!npc.isActiveAndEnabled||npc.gameObject==player.gameObject||forbiddenTalk.Contains(npc.GetInstanceID()))continue;
                var col=npc.GetComponents<Collider>().FirstOrDefault(c=>c.isTrigger);float d=col?Vector3.Distance(col.ClosestPoint(player.transform.position),player.transform.position):Vector3.Distance(npc.transform.position,player.transform.position);
                float range=Mathf.Max(1,player.InteractionRange)+(npc==previous?.4f:0);
                if(d<range&&d<closest){closest=d;NearestTalk=npc;}
                if(allowedFollow.TryGetValue(npc.GetInstanceID(),out bool follow)&&follow&&!TalkingOrCinematic())visuals.FollowNpc(npc,player.transform);
            }
            var target=objectives.ActiveTalk?.Object("NPCTalkTo");var objectiveNpc=target?target.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType=="NPC_Talk"):null;
            if(objectiveNpc&&objectiveNpc.isActiveAndEnabled&&objectiveNpc.Flag("enableObjectiveTalkKeyFallback",true))
            {
                // A key/button fallback must not bypass the authored approach challenge.
                // Timeline StartTalk signals remain independent of this user-input gate.
                float distance=Mathf.Min(Mathf.Max(1,objectiveNpc.Number("objectiveTalkFallbackDistance",100)),Mathf.Max(1,player.InteractionRange));
                var trigger=objectiveNpc.GetComponents<Collider>().FirstOrDefault(c=>c.enabled&&c.isTrigger);
                float separation=trigger?Vector3.Distance(trigger.ClosestPoint(player.transform.position),player.transform.position):Vector3.Distance(player.transform.position,target.transform.position);
                if(separation<=distance&&!forbiddenTalk.Contains(objectiveNpc.GetInstanceID()))NearestTalk=objectiveNpc;
            }
            if(NearestTalk&&player.CanControl&&!TalkingOrCinematic()&&Keyboard.current?.eKey.wasPressedThisFrame==true)
            {if(!interaction||!interaction.HasMenuFor(NearestTalk))Call(NearestTalk,"StartTalk");else interaction.TryTalk();}
            // Q is the second verb on the interaction chips (Observe).
            if(NearestTalk&&player.CanControl&&!TalkingOrCinematic()&&Keyboard.current?.qKey.wasPressedThisFrame==true&&interaction)interaction.TryObserve();
        }
        void ObserveHandoff(bool advance)
        {
            if(ShuttingDown||!HandoffStarted||!heartTransition)return;
            if(!handoffLocked)
            {
                handoffLocked=true;var menu=GetComponent<OriginalMenuUi>();if(menu&&menu.IsOpen)menu.HidePanel();
                if(player){player.InputLocked=true;player.ReleaseTouchInput();}GetComponent<OriginalMobileControls>()?.ReleaseAll();NearestTalk=null;
            }
            if(heartTransition.director)handoffPeak=Mathf.Max(handoffPeak,(float)heartTransition.director.time);
            if(advance&&heartTransition.Playing)handoffElapsed+=Time.deltaTime;
            // Stop resets native director.time to zero. Retain observed progress and
            // played game-time so a sparse final frame still proves the full ending;
            // an early Stop never becomes a completed handoff merely by waiting.
            double duration=HandoffDuration;
            if(!heartTransition.Playing&&duration>0&&(handoffPeak>=duration-.35||handoffElapsed>=duration-.35))handoffCompleted=true;
        }
        void LateUpdate(){ObserveHandoff(false);}
        public AsyncOperation RestartIncludedScene()
        {
            if(!HandoffCompleted||ShuttingDown)return null;string path=gameObject.scene.path;if(string.IsNullOrEmpty(path))return null;
            GetComponent<OriginalMobileControls>()?.ReleaseAll();Shutdown();return SceneManager.LoadSceneAsync(path,LoadSceneMode.Single);
        }
        public void Shutdown(){ShuttingDown=true;Started=false;if(crowd)crowd.Stop();story.StopGraph();}
        void OnApplicationQuit(){ShuttingDown=true;}
        void OnDestroy(){ShuttingDown=true;Started=false;if(Instance==this)Instance=null;}
    }
}
