using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sparc.Original.Tests
{
    public sealed class OriginalInteractionTests
    {
        OriginalRuntime runtime;OriginalBehaviour controller,talk;Renderer[] renderers;Material[][] materials;
        readonly Dictionary<int,string[]> sourceCallbacks=new();
        static string[] Calls(Button b)=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Select(i=>(b.onClick.GetPersistentTarget(i)?b.onClick.GetPersistentTarget(i).GetInstanceID():0)+"/"+b.onClick.GetPersistentMethodName(i)+"/"+b.onClick.GetPersistentListenerState(i)).ToArray();
        void Loaded(Scene scene,LoadSceneMode mode)
        {foreach(var c in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<OriginalBehaviour>(true)).Where(c=>c.sourceType=="InteractionLineController"))foreach(var button in c.Object("InteractionOptionButtonGroup").GetComponentsInChildren<Button>(true))sourceCallbacks[button.GetInstanceID()]=Calls(button);}
        [UnitySetUp] public IEnumerator Setup()
        {
            SceneManager.sceneLoaded+=Loaded;yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");SceneManager.sceneLoaded-=Loaded;yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);runtime.interaction.Initialize();
            controller=runtime.OfType("InteractionLineController").Single(c=>c.sourceId=="1614437575");talk=controller.GetComponents<OriginalBehaviour>().Single(c=>c.sourceType=="NPC_Talk");
            renderers=controller.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer||r is SkinnedMeshRenderer).ToArray();materials=renderers.Select(r=>r.sharedMaterials.ToArray()).ToArray();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Interaction cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);}
        IEnumerator Ready()
        {
            runtime.Begin();foreach(var d in runtime.directors){d.authoredPlayOnAwake=false;d.director.Stop();}runtime.story.StopGraph();runtime.presentation.CancelSpeech();runtime.motion.enabled=false;runtime.crowd.Stop();
            foreach(var agent in UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include,FindObjectsSortMode.None))agent.enabled=false;
            foreach(var c in runtime.components.Where(c=>c&&c.sourceType.StartsWith("Objective",StringComparison.Ordinal)&&c.sourceType!="ObjectiveHUDManager"))c.gameObject.SetActive(false);
            for(var t=talk.transform;t;t=t.parent)t.gameObject.SetActive(true);for(var t=runtime.player.transform;t;t=t.parent)t.gameObject.SetActive(true);
            runtime.player.GetComponent<Rigidbody>().isKinematic=true;runtime.player.InputLocked=false;
            var brain=UnityEngine.Object.FindFirstObjectByType<Cinemachine.CinemachineBrain>();if(brain)brain.enabled=false;
            runtime.player.transform.position=talk.transform.position+Vector3.forward*1.4f;Physics.SyncTransforms();
            var camera=Camera.main;if(camera){camera.transform.position=talk.transform.position+new Vector3(0,2,5);camera.transform.LookAt(talk.transform.position+Vector3.up);}
            runtime.Call(talk,"Allow2Talk2Player");runtime.Call(controller,"ReleaseInteractionUi",true);
            yield return null;yield return new WaitForSecondsRealtime(.75f);
        }
        [UnityTest] public IEnumerator ThreeOriginalOptionGroupsAndReferencesAreRestored()
        {
            Assert.That(runtime.interaction.RestoredControllers,Is.EqualTo(3));
            foreach(var c in runtime.OfType("InteractionLineController"))
            {Assert.That(c.Object("menuInstance"),Is.Not.Null);Assert.That(c.Component<LineRenderer>("lineRenderer"),Is.Not.Null);var buttons=c.Object("InteractionOptionButtonGroup").GetComponentsInChildren<Button>(true);Assert.That(buttons.Length,Is.EqualTo(2),"Keep both native Talk and Observe buttons within their original group");foreach(var button in buttons)CollectionAssert.AreEqual(sourceCallbacks[button.GetInstanceID()],Calls(button),"Keep actual scene-instance persistent callbacks, including prefab overrides");}
            Assert.That(Resources.Load<Shader>("MeetingCellsHighlight").isSupported,Is.True);yield return null;
        }
        [UnityTest] public IEnumerator ApproachingShowsHighlightConnectorAndOptionsAndLeavingRestoresMaterials()
        {
            yield return Ready();Assert.That(runtime.NearestTalk,Is.SameAs(talk));Assert.That(runtime.interaction.MenuVisible,Is.True);Assert.That(runtime.interaction.HighlightVisible,Is.True);Assert.That(controller.Component<LineRenderer>("lineRenderer").enabled,Is.True);
            Assert.That(renderers.Length,Is.GreaterThan(0));for(int i=0;i<renderers.Length;i++)Assert.That(renderers[i].sharedMaterials.Length,Is.EqualTo(materials[i].Length+1));
            runtime.player.transform.position=talk.transform.position+Vector3.right*100;Physics.SyncTransforms();yield return null;yield return null;
            Assert.That(controller.Object("menuInstance").activeSelf,Is.False);Assert.That(controller.Component<LineRenderer>("lineRenderer").enabled,Is.False);for(int i=0;i<renderers.Length;i++)CollectionAssert.AreEqual(materials[i],renderers[i].sharedMaterials);
        }
        [UnityTest] public IEnumerator OriginalObserveAndTalkButtonsEnterTheirSourceBlocksAndSuppressSelection()
        {
            yield return Ready();var buttons=controller.Object("InteractionOptionButtonGroup").GetComponentsInChildren<Button>(true);Assert.That(runtime.interaction.MenuVisible,Is.True);
            buttons[1].onClick.Invoke();yield return null;Assert.That(runtime.story.Trace.Any(t=>t.StartsWith(talk.Text("ObserveBlockName")+"/",StringComparison.Ordinal)),Is.True,"Observe must enter the original source graph");Assert.That(controller.Object("menuInstance").activeSelf,Is.False);
            runtime.story.StopGraph();runtime.presentation.CancelSpeech();foreach(var d in runtime.directors)d.director.Stop();runtime.player.InputLocked=false;runtime.Call(controller,"ReleaseInteractionUi",true);yield return new WaitForSecondsRealtime(.75f);
            Assert.That(runtime.interaction.TryTalk(),Is.True);yield return new WaitForSeconds(.35f);Assert.That(runtime.story.Trace.Any(t=>t.StartsWith(talk.Text("npcTalk_blockName")+"/",StringComparison.Ordinal)),Is.True,"Talk keeps the native delay and original NPC block");Assert.That(runtime.interaction.HighlightVisible,Is.False);
        }
    }
}
