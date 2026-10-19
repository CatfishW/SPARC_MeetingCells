using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sparc.Original.Tests
{
    public sealed class OriginalPlayerCameraTests
    {
        OriginalRuntime runtime;Rigidbody body;CapsuleCollider capsule;
        CinemachineBrain brain;CinemachineVirtualCamera first,third;
        Cinemachine3rdPersonFollow follow;CinemachineCollider avoidance;
        int modelBit,otherMask;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);
            body=runtime.player.GetComponent<Rigidbody>();capsule=runtime.player.GetComponent<CapsuleCollider>();
            first=runtime.player.firstPerson.GetComponent<CinemachineVirtualCamera>();third=runtime.player.thirdPerson.GetComponent<CinemachineVirtualCamera>();
            follow=third.GetCinemachineComponent<Cinemachine3rdPersonFollow>();avoidance=third.GetComponent<CinemachineCollider>();
            brain=UnityEngine.Object.FindFirstObjectByType<CinemachineBrain>();Assert.That(brain,Is.Not.Null);Assert.That(brain.OutputCamera,Is.Not.Null);
            modelBit=1<<LayerMask.NameToLayer("PlayerModel");otherMask=brain.OutputCamera.cullingMask&~modelBit;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Player camera cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);
        }
        IEnumerator Ready(bool isolated)
        {
            runtime.Begin();foreach(var d in runtime.directors){d.authoredPlayOnAwake=false;d.director.Stop();}
            runtime.story.StopGraph();runtime.presentation.CancelSpeech();runtime.motion.enabled=false;
            foreach(var c in runtime.components.Where(c=>c&&c.sourceType.StartsWith("Objective",StringComparison.Ordinal)&&c.sourceType!="ObjectiveHUDManager"))c.gameObject.SetActive(false);
            foreach(var c in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(c!=capsule&&(isolated||c.isTrigger||(c.attachedRigidbody&&c.attachedRigidbody!=body)||c.GetComponentInParent<NavMeshAgent>()))c.enabled=false;
            foreach(var camera in UnityEngine.Object.FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(camera!=first&&camera!=third)camera.enabled=false;
            for(var t=runtime.player.transform;t;t=t.parent)t.gameObject.SetActive(true);
            for(var t=brain.transform;t;t=t.parent)t.gameObject.SetActive(true);
            first.enabled=true;third.enabled=true;runtime.player.firstPerson.SetActive(true);runtime.player.thirdPerson.SetActive(false);
            runtime.player.ShowModel(false);runtime.player.InputLocked=false;capsule.enabled=true;
            body.isKinematic=false;body.useGravity=true;body.linearVelocity=Vector3.zero;
            if(isolated)
            {
                Place(new Vector3(1000,1000,1000),Quaternion.identity);
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Camera fixture ground";
                ground.transform.localScale=new Vector3(40,1,40);
                ground.transform.position=body.position+Vector3.up*(capsule.center.y-capsule.height*.5f-.5f);
            }
            first.PreviousStateIsValid=false;third.PreviousStateIsValid=false;Physics.SyncTransforms();yield return null;
            Assert.That(runtime.player.CanControl,Is.True);NativeRigContract();
        }
        void NativeRigContract()
        {
            Assert.That(runtime.player.CompareTag("Player"),Is.True);Assert.That(runtime.player.gameObject.layer,Is.Zero);
            Assert.That(follow.IgnoreTag,Is.EqualTo(runtime.player.tag));Assert.That(avoidance.m_IgnoreTag,Is.EqualTo(runtime.player.tag));
            Assert.That(follow.CameraCollisionFilter.value,Is.EqualTo(1));Assert.That(avoidance.m_CollideAgainst.value,Is.EqualTo(1));
            Assert.That(follow.CameraDistance,Is.EqualTo(2));Assert.That(third.Follow.name,Is.EqualTo("PlayerCameraRoot"));Assert.That(third.LookAt,Is.SameAs(runtime.player.transform));
            Assert.That(third.m_Lens.FieldOfView,Is.EqualTo(60));Assert.That(third.m_Lens.NearClipPlane,Is.EqualTo(.1f));Assert.That(brain.m_DefaultBlend.m_Time,Is.EqualTo(2));
            Assert.That(capsule.radius,Is.EqualTo(.2f));Assert.That(capsule.height,Is.EqualTo(1.86f));Assert.That(capsule.center.y,Is.EqualTo(.85f));
        }
        void Place(Vector3 position,Quaternion rotation)
        {body.position=position;body.transform.position=position;body.rotation=rotation;body.transform.rotation=rotation;Physics.SyncTransforms();}
        float HorizontalDistance()=>Vector3.ProjectOnPlane(brain.OutputCamera.transform.position-runtime.player.transform.position,Vector3.up).magnitude;
        bool ModelVisible=>(brain.OutputCamera.cullingMask&modelBit)!=0;
        void SafeOutput(string phase)
        {
            var position=brain.OutputCamera.transform.position;float separation=Vector3.Distance(capsule.ClosestPoint(position),position);
            Assert.That(brain.OutputCamera.cullingMask&~modelBit,Is.EqualTo(otherMask),phase+" must preserve all other camera mask bits");
            if(separation<.30f)Assert.That(ModelVisible,Is.False,phase+" output is inside/near the native capsule: "+position+" separation="+separation);
            Assert.That(runtime.player.model.activeInHierarchy,Is.True,"Visibility protection must not disable the native animated rig");
        }
        [UnityTest] public IEnumerator NativeBlendIdleWalkingAndJumpNeverExposeTheInsideOfThePlayer()
        {
            yield return Ready(true);runtime.player.TogglePerspective();bool hiddenDuringBlend=false;
            float until=Time.time+2.5f;bool captured=false;
            while(Time.time<until)
            {
                SafeOutput("Native first-to-third blend");hiddenDuringBlend|=!ModelVisible;
                if(!captured&&brain.IsBlending){Capture("native-blend");captured=true;}yield return null;
            }
            Assert.That(hiddenDuringBlend,Is.True,"The authored two-second blend starts at the first-person head position");
            Assert.That(brain.IsLive(third),Is.True);Assert.That(HorizontalDistance(),Is.GreaterThan(1));Assert.That(ModelVisible,Is.True);Capture("native-clear-idle");
            var before=body.position;until=Time.time+.8f;
            while(Time.time<until){runtime.player.TouchMove=Vector2.up;SafeOutput("Walking");Assert.That(HorizontalDistance(),Is.GreaterThan(1));yield return null;}
            runtime.player.TouchMove=Vector2.zero;Assert.That(Vector3.Distance(before,body.position),Is.GreaterThan(1));Capture("native-clear-walk");
            yield return new WaitForSeconds(.3f);float floor=body.position.y,peak=floor;runtime.player.Jump();until=Time.time+1.1f;
            while(Time.time<until){peak=Mathf.Max(peak,body.position.y);SafeOutput("Jumping");Assert.That(HorizontalDistance(),Is.GreaterThan(1));yield return null;}
            Assert.That(peak-floor,Is.GreaterThan(.3f),"Use the actual native body's jump, not an Animator-only pose");
            runtime.player.ShowModel(false);yield return new WaitForSeconds(.2f);Assert.That(ModelVisible,Is.False,"Camera callbacks must respect source hide intent");
            runtime.player.ShowModel(true);yield return new WaitForSeconds(.2f);Assert.That(ModelVisible,Is.True,"Safe source show intent must restore the rig");NativeRigContract();
        }
        [UnityTest] public IEnumerator NativeWorldCollisionStillWorksAndCloseWallsHideThenRestoreOnlyTheModelLayer()
        {
            yield return Ready(true);runtime.player.TogglePerspective();yield return new WaitForSeconds(2.5f);
            float clear=HorizontalDistance();Assert.That(clear,Is.GreaterThan(2));Assert.That(ModelVisible,Is.True);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Default-layer camera obstruction";wall.layer=0;
            wall.transform.localScale=new Vector3(4,4,.2f);wall.transform.position=body.position+Vector3.up*1.5f-Vector3.forward*1.4f;Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);float blocked=HorizontalDistance();SafeOutput("World wall");
            Assert.That(blocked,Is.LessThan(clear-.4f),"Both native world collision masks must remain functional");
            Assert.That(brain.OutputCamera.transform.position.z,Is.GreaterThan(wall.transform.position.z),"Camera must remain on the player side of the Default-layer wall");
            wall.transform.position=body.position+Vector3.up*1.5f-Vector3.forward*.6f;Physics.SyncTransforms();yield return new WaitForSeconds(.4f);
            SafeOutput("Close world wall");Assert.That(ModelVisible,Is.False,"A legitimate wall may push the native camera into the avatar; only its render layer should disappear");Capture("native-close-wall");
            wall.SetActive(false);yield return new WaitForSeconds(2.7f);Assert.That(HorizontalDistance(),Is.GreaterThan(2));Assert.That(ModelVisible,Is.True);NativeRigContract();
        }
        [UnityTest] public IEnumerator ConversationCamerasKeepTheirSpeakerWhileExplorerRigsClearCloseCharacters()
        {
            yield return Ready(true);brain.m_DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut,0);brain.m_CustomBlends=null;yield return null;
            var eye=brain.OutputCamera.transform;
            // A small animated character, the kind clearance considers, right inside the explorer's lens.
            var character=GameObject.CreatePrimitive(PrimitiveType.Cube);character.name="Clearance fixture character";character.AddComponent<Animator>();
            UnityEngine.Object.Destroy(character.GetComponent<Collider>());character.transform.localScale=Vector3.one*.4f;var renderer=character.GetComponent<Renderer>();
            character.transform.position=eye.position+eye.forward*.4f;yield return new WaitForSeconds(2.3f); // candidates are rescanned every 2 s
            character.transform.position=eye.position+eye.forward*.4f;yield return null;yield return null;
            Assert.That(renderer.forceRenderingOff,Is.True,"A character inside the explorer's own lens is cleared");
            // White's conversation camera sits within reach of White. An NPC camera framing its speaker up close keeps it in shot.
            var shot=new GameObject("Conversation fixture camera").AddComponent<CinemachineVirtualCamera>();shot.Priority=1000;
            shot.transform.SetPositionAndRotation(eye.position+Vector3.right*3,Quaternion.LookRotation(Vector3.forward));
            character.transform.position=shot.transform.position+Vector3.forward*.4f;yield return null;yield return null;yield return null;
            Assert.That(brain.ActiveVirtualCamera?.VirtualCameraGameObject,Is.SameAs(shot.gameObject));
            Assert.That(Vector3.Distance(brain.OutputCamera.transform.position,shot.transform.position),Is.LessThan(.05f));
            Assert.That(renderer.forceRenderingOff,Is.False,"NPC and conversation cameras frame their speaker; clearance must not hide it");
            Assert.That(runtime.player.ClearedRenderers,Is.Empty);
            UnityEngine.Object.Destroy(shot.gameObject);yield return null;yield return null;
            character.transform.position=brain.OutputCamera.transform.position+brain.OutputCamera.transform.forward*.4f;yield return null;yield return null;
            Assert.That(renderer.forceRenderingOff,Is.True,"Back on the explorer's rig, clearance resumes");
            UnityEngine.Object.Destroy(character);
        }
        [UnityTest] public IEnumerator SpokenLinesKeepEveryCharacterInShotThenExplorerClearanceResumes()
        {
            yield return Ready(true);var eye=brain.OutputCamera.transform;
            var character=GameObject.CreatePrimitive(PrimitiveType.Cube);character.name="Clearance fixture character";character.AddComponent<Animator>();
            UnityEngine.Object.Destroy(character.GetComponent<Collider>());character.transform.localScale=Vector3.one*.4f;var renderer=character.GetComponent<Renderer>();
            character.transform.position=eye.position+eye.forward*.4f;yield return new WaitForSeconds(2.3f); // candidates are rescanned every 2 s
            character.transform.position=eye.position+eye.forward*.4f;yield return null;yield return null;
            Assert.That(renderer.forceRenderingOff,Is.True,"Exploring: a character inside the explorer's lens is cleared");
            // A guide's line on screen, even on the explorer's own rig: the shot is authored, nothing is cleared.
            var presentation=typeof(OriginalPresentation);
            presentation.GetProperty("Speaker").SetValue(runtime.presentation,"WhiteBloodCell");presentation.GetProperty("Line").SetValue(runtime.presentation,"Hey there! I'm White.");presentation.GetProperty("IsSpeaking").SetValue(runtime.presentation,true);
            yield return null;yield return null;
            Assert.That(renderer.forceRenderingOff,Is.False,"While a line is spoken every character stays in shot");Assert.That(runtime.player.ClearedRenderers,Is.Empty);
            runtime.presentation.CancelSpeech();yield return null;yield return null;
            Assert.That(renderer.forceRenderingOff,Is.True,"After the line, explorer clearance resumes");
            UnityEngine.Object.Destroy(character);
        }
        [UnityTest] public IEnumerator CapturedSourceWorldIdleAndJumpPositionsRemainSafeWithoutChangingNativeObstacles()
        {
            yield return Ready(false);body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;
            Assert.That(UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Any(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==0),Is.True,"Retain the source world geometry in this fixture");
            runtime.player.TogglePerspective();
            var poses=new[]{new Vector3(46.4400024f,5.57376575f,.3430786f),new Vector3(46.4400024f,6.25441265f,-3.7878418f)};
            for(int i=0;i<poses.Length;i++)
            {
                Place(poses[i],Quaternion.Euler(0,270,0));follow.IgnoreTag="";avoidance.m_IgnoreTag="";third.PreviousStateIsValid=false;
                yield return new WaitForSeconds(2.5f);LogPose("captured-"+i+"-unfiltered");
                follow.IgnoreTag=runtime.player.tag;avoidance.m_IgnoreTag=runtime.player.tag;third.PreviousStateIsValid=false;
                yield return new WaitForSeconds(2.5f);LogPose("captured-"+i+"-player-ignored");SafeOutput("Captured source world pose "+i);NativeRigContract();Capture("native-captured-world-pose-"+i);
            }
        }
        [UnityTest] public IEnumerator QuizAndPeerFormsHideTheAvatarAndSuspendWorldControlsWithoutDisablingTheRig()
        {
            yield return Ready(true);runtime.player.TogglePerspective();yield return new WaitForSeconds(2.5f);
            Assert.That(ModelVisible,Is.True);
            var forms=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.Canvas>(true)).Where(c=>c.name is "QuestionCanvas" or "PlayerInputField" or "AnswerField" or "WaitForResponse").ToArray();
            foreach(var form in forms)
            {
                for(var t=form.transform;t;t=t.parent)t.gameObject.SetActive(true);runtime.activityUi.PreparePanel(form);
                // The native EnableDelay briefly disables PlayerInputField on entry.
                // Audit its actual visible state after the authored delay completes.
                float until=Time.time+6;while(!form.isActiveAndEnabled&&Time.time<until)yield return null;Assert.That(form.isActiveAndEnabled,Is.True,form.name+" did not finish its native entrance");yield return null;
                Assert.That(runtime.player.CanControl,Is.False,form.name+" must own input while its form is visible");Assert.That(ModelVisible,Is.False,form.name+" must not be obscured by the player's head");Assert.That(runtime.player.model.activeInHierarchy,Is.True);form.gameObject.SetActive(false);yield return null;
            }
            Assert.That(runtime.player.CanControl,Is.True);Assert.That(ModelVisible,Is.True);
        }
        void LogPose(string label)
        {
            var camera=brain.OutputCamera;var nearby=Physics.OverlapSphere(camera.transform.position,.4f,1,QueryTriggerInteraction.Ignore).Take(6).Select(c=>c.name+"/"+c.GetType().Name+"/"+c.tag);
            Debug.Log("ORIGINAL_CAMERA "+label+" player="+runtime.player.transform.position+" camera="+camera.transform.position+" horizontal="+HorizontalDistance()+" capsuleSeparation="+Vector3.Distance(camera.transform.position,capsule.ClosestPoint(camera.transform.position))+" raw="+third.State.RawPosition+" correction="+third.State.PositionCorrection+" modelVisible="+ModelVisible+" nearby="+string.Join("|",nearby));
        }
        void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var camera=brain.OutputCamera;var oldTarget=camera.targetTexture;float oldAspect=camera.aspect;var oldActive=RenderTexture.active;
            var target=new RenderTexture(1440,900,24);var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                target.Create();camera.targetTexture=target;camera.aspect=1440f/900;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/CameraFollowup"));Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=oldActive;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);}
        }
    }
}
