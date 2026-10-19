using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sparc.Original.Tests
{
    public sealed class OriginalTalkProximityTests
    {
        OriginalRuntime runtime;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Talk proximity cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);
        }
        [UnityTest] public IEnumerator FarObjectiveCannotOfferTalkButNearTriggerAndAuthoredSignalsStillWork()
        {
            runtime.Begin();
            foreach(var director in runtime.directors)if(director){director.authoredPlayOnAwake=false;if(director.director)director.director.Stop();}
            runtime.story.StopGraph();runtime.presentation.CancelSpeech();
            var red=runtime.OfType("NPC_Talk").Single(c=>c.Text("npcTalk_blockName")=="RBC_new");
            foreach(var other in runtime.OfType("NPC_Talk").Where(c=>c!=red))runtime.Call(other,"DontAllow2Talk2Player");
            foreach(var objective in runtime.OfType("ObjectiveTalkWithNPC"))objective.gameObject.SetActive(false);
            var target=runtime.OfType("ObjectiveTalkWithNPC").Single(c=>c.Object("NPCTalkTo")==red.gameObject);
            for(var parent=red.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            for(var parent=runtime.player.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            for(var parent=target.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            runtime.objectives.Activated(target);runtime.Call(red,"Allow2Talk2Player");
            var agent=red.GetComponent<NavMeshAgent>();if(agent)agent.enabled=false;
            var trigger=red.GetComponents<Collider>().First(c=>c.enabled&&c.isTrigger);
            // A native-scene selection fixture: keep unrelated trigger objectives and
            // physics from completing while testing the actual Runtime.Update gate.
            foreach(var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(collider!=trigger)collider.enabled=false;
            var body=runtime.player.GetComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.None;
            Physics.SyncTransforms();var far=trigger.bounds.center+new Vector3(200,20,0);
            Place(body,far);yield return new WaitForFixedUpdate();yield return null;
            Assert.That(runtime.objectives.ActiveTalk,Is.SameAs(target));
            Assert.That(runtime.NearestTalk,Is.Null,"The source's generous objective fallback must not offer E/Talk from far away");
            Place(body,trigger.bounds.center);yield return new WaitForFixedUpdate();yield return null;
            float separation=Vector3.Distance(trigger.ClosestPoint(runtime.player.transform.position),runtime.player.transform.position);
            Assert.That(red.isActiveAndEnabled,Is.True);Assert.That(separation,Is.LessThan(.01f),"The fixture must actually be inside the live source trigger");
            var menu=runtime.GetComponent<OriginalMenuUi>();Assert.That(menu&&menu.IsOpen,Is.False,"An open menu intentionally suppresses the nearby prompt");
            Assert.That(runtime.NearestTalk,Is.SameAs(red),"Standing inside the actual original trigger must offer the native NPC; player="+runtime.player.transform.position+" body="+body.position+" trigger="+trigger.bounds.center);
            runtime.Call(red,"DontAllow2Talk2Player");yield return null;yield return null;
            Assert.That(runtime.NearestTalk,Is.Null,"The objective fallback must respect the source talk-permission state");
            Place(body,far);yield return new WaitForFixedUpdate();yield return null;
            // StartTalk is also an original Timeline signal, so it must remain able to
            // enter its authored graph independently of user proximity/permission.
            Assert.That(runtime.Call(red,"StartTalk"),Is.True);yield return new WaitForSeconds(.45f);
            Assert.That(runtime.story.Trace.Any(t=>t.StartsWith("RBC_new/",StringComparison.Ordinal)&&t.EndsWith("/Say",StringComparison.Ordinal)),Is.True);
            Assert.That(runtime.presentation.IsSpeaking,Is.True);Assert.That(runtime.presentation.Speaker,Is.EqualTo("RedBloodCell"));
            Assert.That(runtime.Unhandled,Is.Empty);
        }
        static void Place(Rigidbody body,Vector3 position)
        {
            // The original driver interpolates its Rigidbody. A fixture that only moves
            // Transform can be rendered back at the previous physics pose before Update.
            body.position=position;body.transform.position=position;Physics.SyncTransforms();
        }
    }
}
