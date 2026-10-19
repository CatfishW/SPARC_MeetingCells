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
    public sealed class OriginalCrowdTests
    {
        OriginalRuntime runtime;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime&&runtime.crowd,Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Crowd cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);
        }
        [UnityTest] public IEnumerator OriginalSceneTemplateSpawnsMovesCapsAndReplenishesWithoutGrowingSceneCaches()
        {
            var source=runtime.First("NPC_spawn");var alternative=runtime.First("RBC_SPAWN_MeetingCells");
            Assert.That(source.isActiveAndEnabled,Is.True);Assert.That(alternative.enabled,Is.False);
            Assert.That(source.Number("spawm_time_value"),Is.EqualTo(1));Assert.That(source.Number("max_amount_of_NPC"),Is.EqualTo(5));
            runtime.Begin();int sourceComponents=runtime.components.Length;
            yield return new WaitForSeconds(6.4f);
            var spawned=runtime.crowd.Actors.Where(a=>a&&a.Spawned).ToArray();
            Assert.That(spawned.Length,Is.EqualTo(5),"The enabled original one-second source must produce five blocking cells");
            Assert.That(runtime.crowd.Spawned,Is.EqualTo(5));Assert.That(alternative.enabled,Is.False,"Do not force-enable the alternative fifty-cell spawner");
            var original=source.Object("NPC_for_continuous_spawn");var collider=original.GetComponent<CapsuleCollider>();var agent=original.GetComponent<NavMeshAgent>();
            foreach(var actor in spawned)
            {
                Assert.That(actor.Source.sourceId,Is.EqualTo(original.GetComponents<OriginalBehaviour>().Single(c=>c.sourceType=="NPC_random_walk").sourceId));
                Assert.That(actor.GetComponent<CapsuleCollider>().radius,Is.EqualTo(collider.radius));Assert.That(actor.GetComponent<CapsuleCollider>().isTrigger,Is.False);
                Assert.That(actor.GetComponent<NavMeshAgent>().speed,Is.EqualTo(agent.speed));Assert.That(actor.GetComponent<OriginalCrowdCollision>(),Is.Not.Null);
                Assert.That(actor.Lifetime,Is.InRange(20f,60f));Assert.That(actor.transform.localScale.x,Is.InRange(.159f,.181f));
                Assert.That(actor.Source.Data["Destination_Points"].Count(),Is.EqualTo(5));
            }
            var moving=spawned.FirstOrDefault(a=>a.NavReady);Assert.That(moving,Is.Not.Null,"A visible reference alone is not restoration: the original agent must bind the rebuilt NavMesh");
            var position=moving.transform.position;yield return new WaitForSeconds(1);
            Assert.That(Vector3.Distance(position,moving.transform.position),Is.GreaterThan(.15f),"Observe actual original-prefab navigation rather than an assigned agent reference");
            UnityEngine.Object.Destroy(spawned[0].gameObject);yield return null;yield return new WaitForSeconds(1.3f);
            Assert.That(runtime.crowd.Current,Is.EqualTo(5));Assert.That(runtime.crowd.Spawned,Is.EqualTo(6),"Destroyed blockers must release the original cap and be replenished");
            Assert.That(runtime.components.Length,Is.LessThanOrEqualTo(sourceComponents),"Transient source proxies must not accumulate in the global story/visual caches");
            Assert.That(runtime.crowd.ActiveEffects,Is.LessThanOrEqualTo(16));Assert.That(runtime.Unhandled,Is.Empty);
        }
        [UnityTest] public IEnumerator ActualBlockingCellContactPushesTheOriginalPlayerWithoutLockingInput()
        {
            runtime.Begin();
            // Collision fixture only: stop the authored intro and isolate the two actual
            // native physics bodies. No browser route or human difficulty claim is made.
            foreach(var director in runtime.directors)if(director){director.authoredPlayOnAwake=false;if(director.director)director.director.Stop();}
            runtime.story.StopGraph();runtime.player.InputLocked=false;runtime.player.ReleaseTouchInput();
            for(var parent=runtime.player.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            if(UnityEngine.EventSystems.EventSystem.current)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            yield return new WaitForSeconds(1.5f);
            var actor=runtime.crowd.Actors.FirstOrDefault(a=>a&&a.Spawned);Assert.That(actor,Is.Not.Null);
            foreach(var other in runtime.crowd.Actors.Where(a=>a&&a!=actor))foreach(var col in other.GetComponentsInChildren<Collider>())col.enabled=false;
            actor.enabled=false;var navigation=actor.GetComponent<NavMeshAgent>();if(navigation)navigation.enabled=false;
            Assert.That(runtime.player.CanControl,Is.True);var playerBody=runtime.player.GetComponent<Rigidbody>();Assert.That(playerBody&&!playerBody.isKinematic,Is.True);
            actor.transform.position=runtime.player.transform.position+Vector3.forward*.35f;Physics.SyncTransforms();
            var before=runtime.player.transform.position;yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();yield return null;
            var collision=actor.GetComponent<OriginalCrowdCollision>();Assert.That(collision.Contacts,Is.GreaterThan(0),"Use real solid-collider contact; do not call the push API directly");
            yield return new WaitForSeconds(.3f);
            Assert.That(collision.Impulses,Is.GreaterThan(0));Assert.That(runtime.player.CrowdVelocity.sqrMagnitude,Is.GreaterThan(.001f));
            Assert.That(Vector3.Distance(before,runtime.player.transform.position),Is.GreaterThan(.05f));
            Assert.That(runtime.player.InputLocked,Is.False);Assert.That(runtime.crowd.Contacts,Is.GreaterThan(0));Assert.That(runtime.Unhandled,Is.Empty);
        }
        [UnityTest] public IEnumerator OriginalBurstUsesItsAuthoredSpeedAndExpiresAfterFiveSeconds()
        {
            runtime.Begin();var source=runtime.OfType("RBCBurst").First(c=>c.Number("floatFrequency")==5&&c.Number("floatAmplitude")>=2);
            var clone=UnityEngine.Object.Instantiate(source.gameObject);clone.name="Original burst lifetime fixture";clone.transform.rotation=Quaternion.identity;clone.SetActive(true);
            float start=clone.transform.position.z;yield return new WaitForSeconds(.4f);
            Assert.That(clone,Is.Not.Null);Assert.That(clone.transform.position.z-start,Is.GreaterThan(2),"The source moveSpeed is eight, not the obsolete speed fallback of one");
            yield return new WaitForSeconds(4.9f);Assert.That(!clone,Is.True,"Original RBCBurst.Start destroys the object after five seconds");
        }
    }
}
