using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sparc.Original.Tests
{
    public sealed class OriginalHandoffTests
    {
        OriginalRuntime runtime;
        [UnitySetUp] public IEnumerator Setup()
        {yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);}
        [UnityTearDown] public IEnumerator Cleanup()
        {if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Handoff cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);}
        static string[] Bindings(OriginalDirector ending)=>ending.director.playableAsset.outputs.Select(o=>o.streamName+"|"+(o.sourceObject?o.sourceObject.GetInstanceID():0)+"|"+(o.sourceObject&&ending.director.GetGenericBinding(o.sourceObject)?ending.director.GetGenericBinding(o.sourceObject).GetInstanceID():0)).ToArray();
        IEnumerator ReadyWorld()
        {
            runtime.Begin();foreach(var d in runtime.directors){d.authoredPlayOnAwake=false;d.director.Stop();}runtime.story.StopGraph();runtime.presentation.CancelSpeech();
            foreach(var objective in runtime.components.Where(c=>c&&c.sourceType.StartsWith("Objective",StringComparison.Ordinal)&&c.sourceType!="ObjectiveHUDManager"))objective.gameObject.SetActive(false);
            foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))collider.enabled=false;
            for(var ancestor=runtime.player.transform;ancestor;ancestor=ancestor.parent)ancestor.gameObject.SetActive(true);
            runtime.player.GetComponent<Rigidbody>().isKinematic=true;runtime.player.InputLocked=false;yield return null;
        }
        OriginalDirector Ending()
        {
            var ending=runtime.directors.Single(d=>d.name=="Storyline_MeetingCellsTransition2Heart");
            for(var ancestor=ending.transform;ancestor;ancestor=ancestor.parent)ancestor.gameObject.SetActive(true);
            Assert.That(ending.Starts,Is.Zero);Assert.That(ending.director.duration,Is.EqualTo(20.316666666666).Within(.001));return ending;
        }
        [UnityTest] public IEnumerator EarlyEndingStopCannotCompleteOrResumeWorldInteraction()
        {
            yield return ReadyWorld();var red=runtime.OfType("NPC_Talk").Single(c=>c.Text("npcTalk_blockName")=="RBC_new");
            foreach(var other in runtime.OfType("NPC_Talk").Where(c=>c!=red))runtime.Call(other,"DontAllow2Talk2Player");
            for(var ancestor=red.transform;ancestor;ancestor=ancestor.parent)ancestor.gameObject.SetActive(true);runtime.Call(red,"Allow2Talk2Player");var agent=red.GetComponent<NavMeshAgent>();if(agent)agent.enabled=false;
            var trigger=red.GetComponents<Collider>().First(c=>c.isTrigger);trigger.enabled=true;var body=runtime.player.GetComponent<Rigidbody>();body.interpolation=RigidbodyInterpolation.None;body.position=trigger.bounds.center;body.transform.position=body.position;Physics.SyncTransforms();yield return null;yield return null;Assert.That(runtime.NearestTalk,Is.SameAs(red));
            var ending=Ending();var asset=ending.director.playableAsset;double duration=ending.director.duration;var bindings=Bindings(ending);var trace=runtime.story.Trace.ToArray();
            runtime.Call(red,"StartTalk");runtime.player.TouchMove=Vector2.one;runtime.player.TouchLook=Vector2.one;ending.director.Play();yield return new WaitForSeconds(.65f);
            Assert.That(runtime.HandoffStarted,Is.True);Assert.That(runtime.HandoffCompleted,Is.False);Assert.That(runtime.player.CanControl,Is.False);Assert.That(runtime.NearestTalk,Is.Null);Assert.That(runtime.player.TouchMove,Is.EqualTo(Vector2.zero));Assert.That(runtime.player.TouchLook,Is.EqualTo(Vector2.zero));
            ending.director.Stop();yield return null;runtime.player.InputLocked=false;runtime.Call(red,"StartTalk");runtime.Call(red,"StartObserve");runtime.GetComponent<OriginalMenuUi>().ShowPanel();yield return new WaitForSeconds(.35f);
            Assert.That(runtime.HandoffPhase,Is.EqualTo("interrupted"));Assert.That(runtime.HandoffCompleted,Is.False);Assert.That(runtime.presentation.CompletionVisible,Is.False);Assert.That(runtime.player.CanControl,Is.False);Assert.That(runtime.NearestTalk,Is.Null);Assert.That(runtime.GetComponent<OriginalMenuUi>().IsOpen,Is.False);CollectionAssert.AreEqual(trace,runtime.story.Trace,"Pending and new NPC talks must not restart the graph after handoff starts");
            // Direct native Jump callbacks must also respect the terminal state,
            // even if a source callback clears its old InputLocked flag.
            body.isKinematic=false;body.useGravity=false;body.linearVelocity=Vector3.zero;var playerCollider=runtime.player.GetComponent<Collider>();playerCollider.enabled=true;Physics.SyncTransforms();
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Handoff jump fixture ground";ground.transform.localScale=new Vector3(10,1,10);ground.transform.position=body.position-Vector3.up*(playerCollider.bounds.extents.y+.5f);Physics.SyncTransforms();
            runtime.player.Jump();yield return new WaitForFixedUpdate();yield return null;Assert.That(body.linearVelocity.y,Is.EqualTo(0).Within(.001f),"No direct Jump after the handoff, including after native Stop");
            Assert.That(ending.director.playableAsset,Is.SameAs(asset));Assert.That(ending.director.duration,Is.EqualTo(duration));CollectionAssert.AreEqual(bindings,Bindings(ending));Assert.That(ending.Starts,Is.EqualTo(1));Assert.That(runtime.RestartIncludedScene(),Is.Null,"No completion/replay after an early stop");
        }
        [UnityTest] public IEnumerator NaturalAuthoredEndingCompletesOnceAndReplayLoadsFreshIncludedScene()
        {
            yield return ReadyWorld();var ending=Ending();var asset=ending.director.playableAsset;var bindings=Bindings(ending);double duration=ending.director.duration;string scenePath=runtime.gameObject.scene.path;var oldRuntime=runtime;
            ending.director.Play();float deadline=Time.realtimeSinceStartup+45;
            while(!runtime.HandoffCompleted&&Time.realtimeSinceStartup<deadline){if(ending.Playing)Assert.That(runtime.presentation.CompletionVisible,Is.False,"The completion card must not cover the authored ending");yield return null;}
            Assert.That(runtime.HandoffCompleted,Is.True);Assert.That(ending.Playing,Is.False);Assert.That(ending.Starts,Is.EqualTo(1));Assert.That(runtime.HandoffPeakTime>=duration-.35||runtime.HandoffElapsed>=duration-.35,Is.True);Assert.That(runtime.HandoffDuration,Is.EqualTo(duration));Assert.That(runtime.player.CanControl,Is.False);Assert.That(runtime.NearestTalk,Is.Null);Assert.That(runtime.presentation.CompletionVisible,Is.True);
            Assert.That(ending.director.playableAsset,Is.SameAs(asset));CollectionAssert.AreEqual(bindings,Bindings(ending));yield return new WaitForSeconds(.35f);Assert.That(ending.Starts,Is.EqualTo(1),"Completion must not replay automatically");
            var reload=runtime.RestartIncludedScene();Assert.That(reload,Is.Not.Null);yield return reload;yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);Assert.That(runtime,Is.Not.SameAs(oldRuntime));Assert.That(runtime.gameObject.scene.path,Is.EqualTo(scenePath));Assert.That(runtime.Started,Is.False);Assert.That(runtime.HandoffStarted,Is.False);Assert.That(runtime.HandoffCompleted,Is.False);Assert.That(runtime.presentation.CompletionVisible,Is.False);Assert.That(runtime.HandoffStarts,Is.Zero);
            var font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");Assert.That(font,Is.Not.Null);Assert.That(font.atlasTextures[0],Is.Not.Null);Assert.That(runtime.GetComponent<OriginalUiTheme>().StyledGraphicCount,Is.GreaterThan(0));Assert.That(runtime.Unhandled,Is.Empty);
        }
        [TestCase(390,844)] [TestCase(844,390)] [TestCase(1440,900)] [TestCase(960,600)]
        public void CompletionCardHasReadableActualTextAndReplayTargets(int width,int height)
        {
            var layout=OriginalPresentation.GetCompletionLayout(width,height);var title=OriginalPresentation.CompletionTitleStyle();var body=OriginalPresentation.CompletionBodyStyle();
            Assert.That(layout.Card.xMin,Is.GreaterThanOrEqualTo(15));Assert.That(layout.Card.yMin,Is.GreaterThanOrEqualTo(15));Assert.That(layout.Card.xMax,Is.LessThanOrEqualTo(width-15));Assert.That(layout.Card.yMax,Is.LessThanOrEqualTo(height-15));
            foreach(var rect in new[]{layout.Title,layout.Body,layout.Replay}){Assert.That(layout.Card.Contains(rect.min),Is.True);Assert.That(layout.Card.Contains(rect.max),Is.True);}
            Assert.That(layout.Title.Overlaps(layout.Body)||layout.Body.Overlaps(layout.Replay)||layout.Title.Overlaps(layout.Replay),Is.False);Assert.That(title.fontSize,Is.GreaterThanOrEqualTo(28));Assert.That(body.fontSize,Is.GreaterThanOrEqualTo(18));Assert.That(layout.Title.height,Is.GreaterThanOrEqualTo(title.CalcHeight(new GUIContent(OriginalPresentation.CompletionTitle),layout.Title.width)));Assert.That(layout.Body.height,Is.GreaterThanOrEqualTo(body.CalcHeight(new GUIContent(OriginalPresentation.CompletionBody),layout.Body.width)));Assert.That(layout.Replay.width,Is.GreaterThanOrEqualTo(44));Assert.That(layout.Replay.height,Is.GreaterThanOrEqualTo(44));
        }
    }
}
