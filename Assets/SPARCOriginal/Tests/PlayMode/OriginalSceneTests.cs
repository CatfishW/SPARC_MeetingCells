using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Sparc.Original.Tests
{
    public sealed class OriginalSceneTests
    {
        OriginalRuntime runtime;
        [UnitySetUp] public IEnumerator Setup()
        {yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);}
        [UnityTearDown] public IEnumerator Cleanup()
        {if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Original scene cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);}
        [UnityTest] public IEnumerator RestoredSceneContainsOriginalGraphAndOriginalPlayerController()
        {
            Assert.That(runtime.story.Blocks.Count(),Is.EqualTo(29));
            Assert.That(runtime.OfType("Say").Count(),Is.EqualTo(107));
            Assert.That(runtime.story.Blocks.Sum(b=>(b.Data["commandList"] as JArray)?.Count??0),Is.EqualTo(416));
            Assert.That(runtime.directors.Length,Is.EqualTo(20));Assert.That(runtime.player,Is.Not.Null);Assert.That(runtime.player.animator,Is.Not.Null);
            Assert.That(runtime.player.animator.runtimeAnimatorController.name,Is.EqualTo("Customized_Character_Anime"));
            Assert.That(runtime.player.animator.enabled,Is.True);Assert.That(runtime.player.animator.parameters.Any(p=>p.name=="IsWalking"),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator OriginalIntroductionAdvancesItsAuthoredTimelineExactlyOnce()
        {
            var intro=runtime.directors.Single(d=>d.name=="StorylineIntro");Assert.That(intro.director.playableAsset,Is.Not.Null);Assert.That(intro.director.duration,Is.EqualTo(24.1).Within(0.001));
            runtime.Begin();yield return new WaitForSeconds(1);Assert.That(intro.Starts,Is.EqualTo(1));Assert.That(intro.director.time,Is.GreaterThan(0.5));
            runtime.Begin();intro.Activate();yield return new WaitForSeconds(0.4f);Assert.That(intro.Starts,Is.EqualTo(1));
            Debug.Log("ORIGINAL_INTRO asset="+intro.director.playableAsset.name+" duration="+intro.director.duration+" time="+intro.director.time+" starts="+intro.Starts);
        }
        [UnityTest] public IEnumerator OriginalWhiteRigActuallyChangesBonePoseAndFiresPunchEvents()
        {
            runtime.Begin();var source=runtime.First("WBCAnimation");Assert.That(source,Is.Not.Null);var animator=source.Component<Animator>("animator");Assert.That(animator,Is.Not.Null);
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;runtime.visuals.White(source,"WBC_Punch");
            yield return new WaitForSeconds(0.15f);var bones=animator.GetComponentsInChildren<Transform>();var rotations=bones.Select(b=>b.localRotation).ToArray();
            yield return new WaitForSeconds(0.4f);int changed=bones.Where((b,i)=>Quaternion.Angle(rotations[i],b.localRotation)>0.1f).Count();
            Debug.Log("ORIGINAL_WHITE poseChanges="+changed+" clip="+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name))+" punchEvents="+runtime.visuals.PunchEvents);
            Assert.That(changed,Is.GreaterThan(2),"A preserved controller reference is not enough: the original bones must move");
            yield return new WaitForSeconds(2);Assert.That(runtime.visuals.PunchEvents,Is.GreaterThan(0),"Original punch AnimationEvents must reach the new handler");
        }
        [UnityTest] public IEnumerator OriginalPlayerWalkAnimationChangesTheActualRig()
        {
            var animator=runtime.player.animator;Assert.That(animator,Is.Not.Null);animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            // Test the same original body at its gameplay activation point, without replacing its rig.
            for(var t=animator.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);
            yield return null;
            Assert.That(animator.gameObject.activeInHierarchy,Is.True);Assert.That(animator.avatar&&animator.avatar.isValid,Is.True);
            Debug.Log("ORIGINAL_PLAYER_RIG controller="+animator.runtimeAnimatorController.name+" human="+animator.isHuman+" avatar="+animator.avatar.name+" state="+animator.GetCurrentAnimatorStateInfo(0).fullPathHash);

            animator.SetBool("IsWalking",true);animator.SetFloat("Speed",1);for(int frame=0;frame<10;frame++)animator.Update(0.1f);var bones=animator.GetComponentsInChildren<Transform>();var before=bones.Select(b=>b.localRotation).ToArray();animator.Update(0.15f);
            int changed=bones.Where((b,i)=>Quaternion.Angle(before[i],b.localRotation)>0.1f).Count();Debug.Log("ORIGINAL_PLAYER_WALK bones="+changed+" clip="+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name)));
            Assert.That(changed,Is.GreaterThan(2));yield return null;
            Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name.Contains("Run",StringComparison.OrdinalIgnoreCase)||c.clip.name.Contains("Walk",StringComparison.OrdinalIgnoreCase)),Is.True,"Moving idle bones are not evidence of a walking animation");
        }
        [UnityTest] public IEnumerator RealPlayerInputAfterOriginalIntroDrivesLocomotion()
        {
            runtime.Begin();float deadline=Time.realtimeSinceStartup+45;
            while(!runtime.story.CompletedBlocks.Contains("Mo")&&Time.realtimeSinceStartup<deadline){if(runtime.presentation.IsSpeaking)runtime.presentation.Advance();yield return new WaitForSeconds(0.1f);}
            Assert.That(runtime.story.CompletedBlocks.Contains("Mo"),Is.True);
            var route=runtime.First("NPCMove2GivenDestination");var talk=route.GetComponents<OriginalBehaviour>().Single(c=>c.sourceType=="NPC_Talk");runtime.Call(talk,"StartTalk");
            deadline=Time.realtimeSinceStartup+65;
            while((!runtime.story.CompletedBlocks.Contains("RBC_CAP_TALK")||runtime.motion.ReachedWaypoints<4||runtime.CinematicActive||runtime.presentation.IsSpeaking)&&Time.realtimeSinceStartup<deadline){if(runtime.presentation.IsSpeaking)runtime.presentation.Advance();yield return new WaitForSeconds(0.1f);}
            Assert.That(runtime.story.CompletedBlocks.Contains("RBC_CAP_TALK"),Is.True,"Wait for the authored handoff rather than a yield between camera/dialogue commands");Assert.That(runtime.motion.ReachedWaypoints,Is.EqualTo(4));Assert.That(runtime.CinematicActive,Is.False);runtime.player.TogglePerspective();yield return new WaitForSeconds(0.3f);Assert.That(runtime.player.CanControl,Is.True);
            Assert.That(runtime.player.animator,Is.Not.Null);Assert.That(runtime.player.animator.transform.IsChildOf(runtime.player.transform),Is.True,"Input must drive the live original player's rig, not a prefab or another actor");
            float nextLog=0;var animator=runtime.player.animator;var body=runtime.player.transform;var before=body.position;bool locomotion=false;
            // Source RBC blockers now move and collide. Try real cardinal inputs rather
            // than assuming the original forward corridor stays empty for this fixture.
            foreach(var input in new[]{Vector2.up,Vector2.right,Vector2.left,Vector2.down})
            {
            for(float elapsed=0;elapsed<2;elapsed+=Time.deltaTime)
            {
                runtime.player.TouchMove=input;
                var clips=animator.GetCurrentAnimatorClipInfo(0);locomotion|=clips.Any(c=>c.clip.name.Contains("Run",StringComparison.OrdinalIgnoreCase)||c.clip.name.Contains("Walk",StringComparison.OrdinalIgnoreCase));
                if(Time.realtimeSinceStartup>nextLog){nextLog=Time.realtimeSinceStartup+0.4f;Debug.Log("ORIGINAL_INPUT_ANIMATION position="+body.position+" velocity="+body.GetComponent<Rigidbody>().linearVelocity+" kinematic="+body.GetComponent<Rigidbody>().isKinematic+" control="+runtime.player.CanControl+" walking="+runtime.player.Walking+" IsWalking="+animator.GetBool("IsWalking")+" Speed="+animator.GetFloat("Speed")+" height="+animator.GetFloat("height")+" boundPlayables="+animator.hasBoundPlayables+" clips="+string.Join(",",clips.Select(c=>c.clip.name)));}
                yield return null;
            }
            if(Vector3.Distance(before,body.position)>1)break;
            }
            runtime.player.TouchMove=Vector2.zero;
            Assert.That(Vector3.Distance(before,body.position),Is.GreaterThan(1));Assert.That(locomotion,Is.True,"Normal driver input, not direct Animator.Update calls, must leave the original idle state");
            var motor=body.GetComponent<Rigidbody>();Assert.That(motor.interpolation,Is.EqualTo(RigidbodyInterpolation.Interpolate));
            float initialYaw=motor.rotation.eulerAngles.y;
            // Sparse real input deltas must accumulate and remain after several
            // interpolation steps, rather than being replaced by an old physics pose.
            for(int i=0;i<10;i++){runtime.player.TouchLook+=new Vector2(10,0);yield return new WaitForSeconds(.08f);}
            yield return new WaitForSeconds(.12f);
            Assert.That(Mathf.DeltaAngle(initialYaw,motor.rotation.eulerAngles.y),Is.EqualTo(100).Within(2),"The interpolated native Rigidbody must retain all touch yaw deltas");
            yield return new WaitForSeconds(.25f);
            Assert.That(Mathf.DeltaAngle(initialYaw,body.eulerAngles.y),Is.EqualTo(100).Within(2),"The visible native rig must retain the same yaw after interpolation settles");
            Debug.Log("ORIGINAL_INPUT_PERSISTENT_YAW physics="+Mathf.DeltaAngle(initialYaw,motor.rotation.eulerAngles.y)+" rendered="+Mathf.DeltaAngle(initialYaw,body.eulerAngles.y));
        }
        [UnityTest] public IEnumerator OriginalWhiteExitButtonRevealsItsOwnConfirmationAndExitsOnce()
        {
            runtime.Begin();var manager=runtime.First("WhiteBloodCellGameManager");for(var t=manager.transform;t;t=t.parent)t.gameObject.SetActive(true);runtime.activities.Activated(manager);yield return new WaitForSeconds(0.3f);
            var game=runtime.activities.Games.Single(g=>g.IsWhite);var canvas=manager.GetComponentInParent<Canvas>();
            var exit=canvas.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="ExitButton"&&b.transform.parent.name=="InstructionPanel");
            foreach(var proxy in runtime.OfType("ExitConfirmation"))Debug.Log("WHITE_EXIT_COMPONENT canvas="+(proxy.GetComponentInParent<Canvas>(true)?.name)+" source="+proxy.sourceId+" active="+proxy.gameObject.activeInHierarchy+" target="+proxy.Object("exitConfirmationPanel"));
            Assert.That(game.Finished,Is.False);exit.onClick.Invoke();yield return new WaitForSeconds(0.2f);
            var prompts=runtime.OfType("ExitConfirmation").Where(c=>c.transform.IsChildOf(canvas.transform)&&runtime.activities.IsExitPromptVisible(c)).ToArray();Assert.That(prompts.Length,Is.EqualTo(1));
            var panel=prompts[0].Object("exitConfirmationPanel");var confirm=panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name.IndexOf("confirm",StringComparison.OrdinalIgnoreCase)>=0||b.name.IndexOf("yes",StringComparison.OrdinalIgnoreCase)>=0);
            confirm.onClick.Invoke();yield return null;confirm.onClick.Invoke();yield return null;Assert.That(game.Continued,Is.True);Assert.That(canvas.gameObject.activeInHierarchy,Is.False);
            Assert.That(runtime.story.Trace.Count(t=>t.StartsWith("WBC_LOSE/",StringComparison.Ordinal)&&t.EndsWith("/Say",StringComparison.Ordinal)),Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator OriginalWordSubmissionCannotBroadcastTheEndingTwice()
        {
            runtime.Begin();var words=runtime.First("WordGameManager");Assert.That(words,Is.Not.Null);
            runtime.activities.Call(words,"Submit",null);runtime.activities.Call(words,"Submit",null);
            Assert.That(runtime.activities.WordCompletionBroadcasts,Is.EqualTo(1));yield return null;
        }
        [UnityTest] public IEnumerator HiddenExitConfirmationCannotSkipTheOriginalActivity()
        {
            runtime.Begin();var exit=runtime.OfType("ExitConfirmation").First();
            for(var parent=exit.transform;parent!=null;parent=parent.parent)parent.gameObject.SetActive(true);
            runtime.activities.Activated(exit);Assert.That(runtime.activities.IsExitPromptVisible(exit),Is.False);
            int before=runtime.story.Trace.Count;runtime.activities.Call(exit,"OnConfirmButton",null);
            Assert.That(runtime.story.Trace.Count,Is.EqualTo(before),"Hidden confirmation must not take a continuation branch");
            runtime.activities.Call(exit,"OnExitButton",null);Assert.That(runtime.activities.IsExitPromptVisible(exit),Is.True);
            runtime.activities.Call(exit,"OnCancelButton",null);Assert.That(runtime.activities.IsExitPromptVisible(exit),Is.False);yield return null;
        }
        [UnityTest] public IEnumerator OriginalCapRouteReturnsControlAndDoesNotReplayItsTutorial()
        {
            runtime.Begin();float deadline=Time.realtimeSinceStartup+38;
            while(!runtime.story.CompletedBlocks.Contains("Mo")&&Time.realtimeSinceStartup<deadline)
            {if(runtime.presentation.IsSpeaking)runtime.presentation.Advance();yield return new WaitForSeconds(0.1f);}
            Assert.That(runtime.story.CompletedBlocks.Contains("Mo"),Is.True);
            var route=runtime.First("NPCMove2GivenDestination");Assert.That(route,Is.Not.Null);
            var talk=route.GetComponents<OriginalBehaviour>().Single(c=>c.sourceType=="NPC_Talk");runtime.Call(talk,"StartTalk");
            deadline=Time.realtimeSinceStartup+50;
            while(!runtime.story.CompletedBlocks.Contains("RBC_CAP_TALK")&&Time.realtimeSinceStartup<deadline)
            {if(runtime.presentation.IsSpeaking)runtime.presentation.Advance();yield return new WaitForSeconds(0.1f);}
            Assert.That(runtime.motion.ReachedWaypoints,Is.EqualTo(4),"Use all four original authored destinations");
            Assert.That(talk.Object("NPC_Follow_VirtualCamera").activeSelf,Is.False,"CAP follow camera must hand control back to the original player");
            Assert.That(talk.Object("NPC_VirtualCamera").activeSelf,Is.False);
            var tutorial=runtime.directors.Single(d=>d.name=="Storyline_introduce_yellow_circle");Assert.That(tutorial.Starts,Is.EqualTo(1));
            Assert.That(runtime.Unhandled,Is.Empty);Debug.Log("ORIGINAL_CAP_ROUTE waypoints="+runtime.motion.ReachedWaypoints+" tutorialStarts="+tutorial.Starts+" playerCameraReturned=true");
        }
        [UnityTest] public IEnumerator OriginalRbcButtonIsVisibleAfterItsWorldHudWasHidden()
        {
            runtime.Begin();yield return new WaitForSeconds(27);var button=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.name=="PlayGameRBC");
            var world=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="GameHUD");world.gameObject.SetActive(false);
            button.gameObject.SetActive(true);yield return new WaitForSeconds(0.3f);
            if(runtime.presentation.IsSpeaking){Assert.That(button.IsInteractable(),Is.False,"A launcher must not compete with spoken dialogue");runtime.presentation.CancelSpeech();yield return new WaitForSeconds(.3f);}
            Assert.That(runtime.activityUi.LauncherCount,Is.EqualTo(2),"Both original launcher buttons must be discovered after scene activation");
            Debug.Log("ORIGINAL_RBC_PROMPT_DIAGNOSTIC self="+button.gameObject.activeSelf+" scene="+button.gameObject.activeInHierarchy+" miniOpen="+runtime.activities.IsOpen+" games="+string.Join(",",runtime.activities.Games.Select(g=>g.name+":"+g.source.isActiveAndEnabled)));
            Assert.That(button.gameObject.activeInHierarchy,Is.True,"The original activeSelf flag alone must not strand the prompt under a hidden parent");
            var canvas=button.GetComponentInParent<Canvas>();Assert.That(canvas&&canvas.isActiveAndEnabled,Is.True);Assert.That(canvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));Assert.That(button.IsInteractable(),Is.True);
            Assert.That(button.onClick.GetPersistentEventCount(),Is.EqualTo(2),"Retain both original authored native callbacks");
            var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
            foreach(var corner in corners){var screen=RectTransformUtility.WorldToScreenPoint(null,corner);Assert.That(screen.x,Is.InRange(0f,(float)Screen.width));Assert.That(screen.y,Is.InRange(0f,(float)Screen.height));}

            button.onClick.Invoke();yield return new WaitForSeconds(0.4f);
            var game=runtime.activities.Games.Single(g=>!g.IsWhite);Assert.That(game.Begun,Is.True);Assert.That(game.Duration,Is.EqualTo(120));Assert.That(game.Area.name,Is.EqualTo("PlayArea"));
            Assert.That(game.Area.rect.width,Is.GreaterThan(300));Assert.That(game.Area.rect.height,Is.GreaterThan(300));Assert.That(game.Player.GetComponent<OriginalMiniPlayer>(),Is.Not.Null);
            yield return new WaitForSeconds(3);Assert.That(UnityEngine.Object.FindObjectsByType<OriginalMiniToken>(FindObjectsSortMode.None).Any(t=>t.game==game&&t.Oxygen),Is.True,"The actual original oxygen prefab must spawn");
            Assert.That(runtime.Unhandled,Is.Empty);Debug.Log("ORIGINAL_RBC_UI originalCallbacks=2 duration="+game.Duration+" area="+game.Area.rect+" originalOxygenSpawned=true");
        }
        [UnityTest] public IEnumerator EscapedOxygenCannotRunForeverOrChangeTheScore()
        {
            runtime.Begin();var source=runtime.First("RedBloodCellGameManager");for(var t=source.transform;t;t=t.parent)t.gameObject.SetActive(true);
            runtime.activities.Activated(source);yield return new WaitForSeconds(3.3f);var game=runtime.activities.Games.Single(g=>!g.IsWhite);
            var token=UnityEngine.Object.FindObjectsByType<OriginalMiniToken>(FindObjectsSortMode.None).First(t=>t.game==game);int score=game.Score;
            token.transform.position=game.Area.TransformPoint(new Vector3(0,game.Area.rect.yMin-game.Area.rect.height*2,0));yield return null;yield return null;
            Assert.That(token==null,Is.True);Assert.That(game.Score,Is.EqualTo(score),"Cleanup must not award oxygen");
            yield return new WaitForSeconds(2.4f);game.End(true);yield return null;
            foreach(var oxygen in UnityEngine.Object.FindObjectsByType<OriginalMiniToken>(FindObjectsSortMode.None).Where(t=>t.game==game))Assert.That(oxygen.GetComponent<Rigidbody2D>().simulated,Is.False,"Victory must stop completed activity physics");
        }
        [UnityTest] public IEnumerator OriginalWhiteActivityUsesItsOriginalArenaAndFiveBacteria()
        {
            runtime.Begin();var manager=runtime.First("WhiteBloodCellGameManager");Assert.That(manager,Is.Not.Null);
            for(var t=manager.transform;t;t=t.parent)t.gameObject.SetActive(true);
            runtime.activities.Activated(manager);
            // Begin spawns synchronously. Inspect its exact initial population before
            // random native movement can legitimately collect one in a physics step.
            var game=runtime.activities.Games.Single(g=>g.IsWhite);Assert.That(game.Begun,Is.True);Assert.That(game.Level,Is.EqualTo(1));Assert.That(game.RemainingHostiles,Is.EqualTo(5));
            yield return new WaitForSeconds(0.2f);
            Assert.That(game.Area.name,Is.EqualTo("PlayArea"));Assert.That(game.Duration,Is.EqualTo(manager.Number("time_limit",60)));Assert.That(runtime.Unhandled,Is.Empty);
        }
        [UnityTest] public IEnumerator OriginalWhitePhysicsCanCollectAHostileRatherThanOnlyTimeOut()
        {
            runtime.Begin();var manager=runtime.First("WhiteBloodCellGameManager");for(var t=manager.transform;t;t=t.parent)t.gameObject.SetActive(true);
            runtime.activities.Activated(manager);yield return new WaitForSeconds(0.3f);
            var game=runtime.activities.Games.Single(g=>g.IsWhite);var player=game.Player.GetComponent<OriginalMiniPlayer>();var body=game.Player.GetComponent<Rigidbody2D>();
            Assert.That(player.source.Flag("allowVerticalMovement"),Is.True,"The original defense player must retain vertical input");
            var hostile=UnityEngine.Object.FindObjectsByType<OriginalMiniToken>(FindObjectsSortMode.None).First(t=>t.game==game&&t.Hostile);
            var target=hostile.GetComponent<Rigidbody2D>();
            foreach(var actor in new[]{game.Player,hostile.gameObject})
            {
                var rb=actor.GetComponent<Rigidbody2D>();
                Debug.Log("ORIGINAL_DEFENSE_PHYSICS "+actor.name+" tag="+actor.tag+" layer="+actor.layer+" body="+rb.bodyType+" gravity="+rb.gravityScale+" scale="+actor.transform.lossyScale+" source="+string.Join("|",actor.GetComponents<OriginalBehaviour>().Where(c=>c.sourceType is "PlayerController2D" or "BacteriaController").Select(c=>c.configJson)));
                foreach(var col in actor.GetComponents<Collider2D>())Debug.Log("ORIGINAL_DEFENSE_COLLIDER "+actor.name+" "+col.GetType().Name+" trigger="+col.isTrigger+" enabled="+col.enabled+" size="+col.bounds.size);
            }
            Assert.That(Physics2D.GetIgnoreLayerCollision(game.Player.layer,hostile.gameObject.layer),Is.False);
            // Isolate the two original physics bodies. Randomly spawned friendly cells may
            // otherwise overlap this fixture's test position and legitimately subtract score.
            // This does not alter the gameplay spawn or grading rules, and never calls Collect.
            var excluded=game.Area.GetComponentsInChildren<Collider2D>().Where(c=>!c.transform.IsChildOf(game.Player.transform)&&!c.transform.IsChildOf(hostile.transform)).ToArray();
            var enabled=excluded.Select(c=>c.enabled).ToArray();for(int i=0;i<excluded.Length;i++)excluded[i].enabled=false;
            game.Transitioning=true;int score=game.Score,remaining=game.RemainingHostiles;int expected=(int)hostile.source.Number("scoreForDestroy",1);Vector2 contact=game.Area.TransformPoint(game.Area.rect.center);
            try
            {
                body.position=contact;target.position=contact;game.Player.transform.position=new Vector3(contact.x,contact.y,game.Player.transform.position.z);hostile.transform.position=new Vector3(contact.x,contact.y,hostile.transform.position.z);Physics2D.SyncTransforms();
                yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();yield return null;
                Assert.That(game.Score,Is.EqualTo(score+expected),"Exactly the original contacted hostile's score must be awarded");
                Assert.That(game.RemainingHostiles,Is.EqualTo(remaining-1));
            }
            finally{for(int i=0;i<excluded.Length;i++)if(excluded[i])excluded[i].enabled=enabled[i];game.Transitioning=false;}
        }
        [UnityTest] public IEnumerator OriginalWhiteInstructionsAreAllPresentWithoutOverlap()
        {
            runtime.Begin();var manager=runtime.First("WhiteBloodCellGameManager");for(var t=manager.transform;t;t=t.parent)t.gameObject.SetActive(true);runtime.activities.Activated(manager);yield return new WaitForSeconds(0.3f);
            var canvas=manager.GetComponentInParent<Canvas>();var panel=canvas.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="InstructionPanel");
            canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="__SPARC_MINI_Help").onClick.Invoke();yield return null;
            string[] names={"TimeText","ScoreText","LevelText","RemText","Instruction0","Instruction1","Instruction2","Instruction3","ExitButton"};
            var rows=names.Select(name=>panel.GetComponentsInChildren<RectTransform>().Single(t=>t.name==name)).ToArray();var rectangles=rows.Select(row=>{var c=new Vector3[4];row.GetWorldCorners(c);return Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));}).ToArray();
            for(int i=0;i<rectangles.Length;i++)Assert.That(rectangles[i].height,Is.GreaterThan(10));
            // Long instructions now scroll inside a real RectMask2D. Check their
            // visible portions rather than treating clipped offscreen content as UI.
            for(int i=0;i<rows.Length;i++)
            {
                var scroll=rows[i].GetComponentInParent<UnityEngine.UI.ScrollRect>();if(!scroll)continue;
                Assert.That(scroll.vertical&&scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>(),Is.True);
                var c=new Vector3[4];scroll.viewport.GetWorldCorners(c);var clip=Rect.MinMaxRect(c.Min(v=>v.x),c.Min(v=>v.y),c.Max(v=>v.x),c.Max(v=>v.y));
                var r=rectangles[i];rectangles[i]=Rect.MinMaxRect(Mathf.Max(r.xMin,clip.xMin),Mathf.Max(r.yMin,clip.yMin),Mathf.Min(r.xMax,clip.xMax),Mathf.Min(r.yMax,clip.yMax));
            }
            for(int i=0;i<rectangles.Length;i++)for(int j=i+1;j<rectangles.Length;j++)if(rectangles[i].width>0&&rectangles[i].height>0&&rectangles[j].width>0&&rectangles[j].height>0)Assert.That(rectangles[i].Overlaps(rectangles[j]),Is.False,names[i]+" visibly overlaps "+names[j]);
            Assert.That(rows[4].GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("You are a white blood cell!"));Assert.That(rows[7].GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("Watch out! Don't destroy red blood cells!"));
        }
        [UnityTest] public IEnumerator OriginalWhiteThreeLevelsPreserveTipsAndWaitForTheOriginalWinButton()
        {
            runtime.Begin();var manager=runtime.First("WhiteBloodCellGameManager");for(var t=manager.transform;t;t=t.parent)t.gameObject.SetActive(true);runtime.activities.Activated(manager);yield return new WaitForSeconds(0.3f);
            var game=runtime.activities.Games.Single(g=>g.IsWhite);var playerBody=game.Player.GetComponent<Rigidbody2D>();var playerCollider=game.Player.GetComponent<Collider2D>();
            for(int level=1;level<=3;level++)
            {
                float deadline=Time.realtimeSinceStartup+8;while(game.Transitioning&&Time.realtimeSinceStartup<deadline)yield return null;Assert.That(game.Level,Is.EqualTo(level));
                // Collision-focused fixture: move each original hostile body into the actual
                // collection collider. This is unit coverage, not a browser-player win claim.
                var hostiles=UnityEngine.Object.FindObjectsByType<OriginalMiniToken>(FindObjectsSortMode.None).Where(t=>t.game==game&&t.Hostile).ToArray();Assert.That(hostiles.Length,Is.EqualTo(5));
                foreach(var hostile in hostiles)
                {
                    if(!hostile)continue;var body=hostile.GetComponent<Rigidbody2D>();body.position=playerCollider.bounds.center;hostile.transform.position=new Vector3(body.position.x,body.position.y,hostile.transform.position.z);Physics2D.SyncTransforms();
                    yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();yield return null;
                }
                deadline=Time.realtimeSinceStartup+8;while(game.Level==level&&!game.Finished&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.That(game.RemainingHostiles==0||game.Level==level+1,Is.True);
                if(level<3)
                {
                    var screen=manager.Object("loadingScreen");Assert.That(screen&&screen.activeInHierarchy,Is.True);
                    var loading=screen.GetComponents<OriginalBehaviour>().Single(c=>c.sourceType=="WhiteCellLoadingScreen");
                    string expected=level==1?"White blood cells are responsible for protecting the body from invaders like bacteria.":"There are different types of white blood cells, each with different jobs.";
                    Assert.That(loading.Component<TMPro.TMP_Text>("loadingText").text,Is.EqualTo(expected),"Show the source tip for the level just completed");
                }
            }
            Assert.That(game.Finished,Is.True);Assert.That(game.Level,Is.EqualTo(3));yield return new WaitForSeconds(5.4f);
            Assert.That(game.Continued,Is.False,"The source win screen must remain until the player presses its original button");
            Assert.That(runtime.story.Trace.Any(t=>t.StartsWith("WBC_WIN/",StringComparison.Ordinal)),Is.False);
            var victory=manager.Object("victoryScreen");Assert.That(victory&&victory.activeInHierarchy,Is.True);
            var continueButton=victory.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="ExitButton");
            Assert.That(Enumerable.Range(0,continueButton.onClick.GetPersistentEventCount()).Any(i=>continueButton.onClick.GetPersistentMethodName(i)=="ContinueAfterWin"),Is.True);
            continueButton.onClick.Invoke();yield return null;continueButton.onClick.Invoke();yield return null;
            Assert.That(game.Continued,Is.True);
            Assert.That(runtime.story.Trace.Count(t=>t.StartsWith("WBC_WIN/",StringComparison.Ordinal)&&t.EndsWith("/Say",StringComparison.Ordinal)),Is.EqualTo(1));
            Assert.That(runtime.Unhandled,Is.Empty);
        }
        [UnityTest] public IEnumerator OriginalWhiteLossShowsItsAuthoredGermsAndContinuesAfterFiveSeconds()
        {
            runtime.Begin();var manager=runtime.First("WhiteBloodCellGameManager");for(var t=manager.transform;t;t=t.parent)t.gameObject.SetActive(true);runtime.activities.Activated(manager);yield return new WaitForSeconds(0.3f);
            var game=runtime.activities.Games.Single(g=>g.IsWhite);var germs=manager.Object("GameObject_To_Activate_When_Lost");
            Assert.That(germs,Is.Not.Null);Assert.That(germs.name,Is.EqualTo("GameLostGerms"));Assert.That(germs.activeSelf,Is.False);
            // Lifecycle fixture only: the full original timer is covered by the real-input browser journey.
            game.End(false);Assert.That(germs.activeSelf,Is.True);Assert.That(game.Finished,Is.True);
            yield return new WaitForSeconds(4.5f);Assert.That(game.Continued,Is.False);
            yield return new WaitForSeconds(0.9f);Assert.That(game.Continued,Is.True);
            Assert.That(runtime.story.Trace.Count(t=>t.StartsWith("WBC_LOSE/",StringComparison.Ordinal)&&t.EndsWith("/Say",StringComparison.Ordinal)),Is.EqualTo(1));
            Assert.That(runtime.Unhandled,Is.Empty);
        }
        [UnityTest] public IEnumerator AllSavedOutfitsPreserveTheOriginalAnimatedRig()
        {
            var appearance=UnityEngine.Object.FindFirstObjectByType<OriginalAppearance>();Assert.That(appearance,Is.Not.Null);yield return null;
            var catalog=JObject.Parse(Resources.Load<TextAsset>("AppearanceCatalog").text);var presets=(JArray)catalog["presets"];
            var animator=runtime.player.animator;var controller=animator.runtimeAnimatorController;var avatar=animator.avatar;
            var renderers=runtime.player.model.GetComponentsInChildren<SkinnedMeshRenderer>(true);var bones=renderers.ToDictionary(r=>r.GetInstanceID(),r=>r.bones);
            int version=1;
            foreach(var preset in presets)
            {
                var body=new JObject{["schemaVersion"]=1,["catalogVersion"]=(string)catalog["catalogVersion"],["presetId"]=(string)preset["id"],["skinIndex"]=(int)preset["skinIndex"],["parts"]=preset["parts"].DeepClone()};
                appearance.ReceiveAppearance(new JObject{["status"]="ready",["revision"]=version++,["appearance"]=body}.ToString());
                Assert.That(appearance.AppliedPreset,Is.EqualTo((string)preset["id"]),"The exact original modular parts must accept every saved canonical outfit");
                var chosen=((JArray)preset["parts"]).Values<string>().ToHashSet(StringComparer.Ordinal);
                Assert.That(renderers.Where(r=>r.gameObject.activeSelf).Select(r=>r.name),Is.EquivalentTo(chosen));
                Assert.That(animator.runtimeAnimatorController,Is.SameAs(controller));Assert.That(animator.avatar,Is.SameAs(avatar));
                foreach(var renderer in renderers)CollectionAssert.AreEqual(bones[renderer.GetInstanceID()],renderer.bones);
            }
            Assert.That(version-1,Is.EqualTo(120));Debug.Log("ORIGINAL_OUTFITS verified="+(version-1)+" rigPreserved=true");
        }
        [UnityTest] public IEnumerator MalformedOrStaleOutfitsCannotMutateTheOriginalAvatar()
        {
            var appearance=UnityEngine.Object.FindFirstObjectByType<OriginalAppearance>();yield return null;
            var catalog=JObject.Parse(Resources.Load<TextAsset>("AppearanceCatalog").text);var preset=catalog["presets"][0];
            var renderers=runtime.player.model.GetComponentsInChildren<SkinnedMeshRenderer>(true);var baseline=renderers.Where(r=>r.gameObject.activeSelf).Select(r=>r.name).ToArray();
            var envelope=new JObject{["status"]="ready",["revision"]=20,["appearance"]=new JObject{["schemaVersion"]=1,["catalogVersion"]=(string)catalog["catalogVersion"],["presetId"]=(string)preset["id"],["skinIndex"]=(int)preset["skinIndex"],["parts"]=preset["parts"].DeepClone()}};
            appearance.ReceiveAppearance(envelope.ToString());Assert.That(appearance.AppliedPreset,Is.EqualTo((string)preset["id"]));var accepted=renderers.Where(r=>r.gameObject.activeSelf).Select(r=>r.name).ToArray();
            var invalid=(JObject)envelope.DeepClone();invalid["revision"]=21;invalid["appearance"]["parts"]=new JArray("UnknownPart");appearance.ReceiveAppearance(invalid.ToString());
            CollectionAssert.AreEquivalent(accepted,renderers.Where(r=>r.gameObject.activeSelf).Select(r=>r.name).ToArray());
            invalid=(JObject)envelope.DeepClone();invalid["revision"]=19;invalid["appearance"]["parts"]=catalog["presets"][1]["parts"].DeepClone();appearance.ReceiveAppearance(invalid.ToString());
            CollectionAssert.AreEquivalent(accepted,renderers.Where(r=>r.gameObject.activeSelf).Select(r=>r.name).ToArray());
            appearance.ReceiveAppearance("{invalid");appearance.ReceiveAppearance(new string('x',32769));
            CollectionAssert.AreEquivalent(accepted,renderers.Where(r=>r.gameObject.activeSelf).Select(r=>r.name).ToArray());
            appearance.ReceiveAppearance("{\"status\":\"signed_out\"}");Assert.That(appearance.AppliedPreset,Is.Empty);
            CollectionAssert.AreEquivalent(baseline,renderers.Where(r=>r.gameObject.activeSelf).Select(r=>r.name).ToArray());
        }
        [UnityTest] public IEnumerator OriginalQuestionChoicesRemainReadableUnderTheirTimelineParent()
        {
            runtime.Begin();var director=runtime.directors.Single(d=>d.name=="Storyline_RBC_Raise_Question");
            for(var p=director.transform;p;p=p.parent)p.gameObject.SetActive(true);
            var canvas=director.GetComponentsInChildren<Canvas>(true).Single(c=>c.name=="QuestionCanvas");canvas.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.5f);
            var buttons=canvas.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.name=="Button A"||b.name=="Button B").ToArray();Assert.That(buttons.Length,Is.EqualTo(2));
            foreach(var button in buttons)
            {
                var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);var points=corners.Select(v=>RectTransformUtility.WorldToScreenPoint(null,v)).ToArray();
                float width=points.Max(v=>v.x)-points.Min(v=>v.x),height=points.Max(v=>v.y)-points.Min(v=>v.y);
                Debug.Log("ORIGINAL_QUESTION_HITBOX "+button.name+" width="+width+" height="+height+" viewport="+Screen.width+"x"+Screen.height);
                Assert.That(width/Screen.width,Is.GreaterThan(0.04f));Assert.That(height/Screen.height,Is.GreaterThan(0.035f));
                Assert.That(points.Min(v=>v.x),Is.GreaterThanOrEqualTo(0));Assert.That(points.Max(v=>v.x),Is.LessThanOrEqualTo(Screen.width));
                Assert.That(button.onClick.GetPersistentEventCount(),Is.GreaterThan(0),"Keep the authored story branch callback");
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=(points[0]+points[2])*0.5f};UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>(),Is.SameAs(button),"Visible choice must receive the actual pointer, not only direct callbacks");
            }
            runtime.activities.Call(runtime.First("RBCQuestion"),"CompleteQuestion",null);yield return null;
        }
        [TestCase("I understand, I choose B", "B")]
        [TestCase("My answer A is correct", "A")]
        [TestCase("I want more details", "")]
        [TestCase("B is right!", "B")]
        [TestCase(null, "")]
        public void OriginalPeerChoiceUsesResponseSignalsNotPlayerAnswerGuessing(string response,string expected)
        {Assert.That(OriginalPeerAgent.Choice(response),Is.EqualTo(expected));}
        [UnityTest] public IEnumerator OriginalOxygenVictoryExitUsesTheVisibleConfirmationOnce()
        {
            runtime.Begin();var manager=runtime.First("RedBloodCellGameManager");Assert.That(manager,Is.Not.Null);
            for(var t=manager.transform;t;t=t.parent)t.gameObject.SetActive(true);
            runtime.activities.Activated(manager);yield return new WaitForSeconds(0.3f);
            var game=runtime.activities.Games.Single(g=>!g.IsWhite);game.End(true);yield return new WaitForSeconds(0.2f);
            var canvas=manager.GetComponentInParent<Canvas>();var victory=manager.Object("victoryScreen");Assert.That(victory&&victory.activeInHierarchy,Is.True);
            var button=victory.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="ExitButton");
            for(int i=0;i<button.onClick.GetPersistentEventCount();i++)Debug.Log("ORIGINAL_VICTORY_EXIT "+button.onClick.GetPersistentMethodName(i)+" -> "+button.onClick.GetPersistentTarget(i));
            button.onClick.Invoke();yield return new WaitForSeconds(0.2f);
            var prompts=runtime.OfType("ExitConfirmation").Where(e=>e.transform.IsChildOf(canvas.transform)&&runtime.activities.IsExitPromptVisible(e)).ToArray();
            Assert.That(prompts.Length,Is.EqualTo(1),"Victory Exit must expose its own original confirmation, not a hidden other minigame's panel");
            var panel=prompts[0].Object("exitConfirmationPanel");var confirm=panel.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b=>b.name.IndexOf("confirm",StringComparison.OrdinalIgnoreCase)>=0||b.name.IndexOf("yes",StringComparison.OrdinalIgnoreCase)>=0);
            Assert.That(confirm,Is.Not.Null,"Keep the original confirmation button");confirm.onClick.Invoke();yield return null;confirm.onClick.Invoke();yield return null;
            Assert.That(game.Continued,Is.True);Assert.That(canvas.gameObject.activeInHierarchy,Is.False);
            Assert.That(runtime.story.Trace.Count(t=>t.StartsWith("AfterRBCMinigame/",StringComparison.Ordinal)&&t.EndsWith("/Say",StringComparison.Ordinal)),Is.EqualTo(1));
            Assert.That(runtime.Unhandled,Is.Empty);
        }
        [UnityTest] public IEnumerator OriginalSceneIntroPlaysForItsFullDuration()
        {
            runtime.Begin();var intro=runtime.directors.Single(d=>d.name=="StorylineIntro");
            yield return new WaitForSeconds(27);
            Debug.Log("ORIGINAL_INTRO_FULL starts="+intro.Starts+" active="+intro.gameObject.activeInHierarchy+" player="+runtime.player.transform.position+" block="+runtime.story.CurrentBlock+" line="+runtime.presentation.Line+" boundaries="+string.Join(";",runtime.Unhandled));
            Assert.That(runtime.presentation.PresentedLines,Is.GreaterThan(0),"The intro must actually enter its original CAP conversation, not merely finish playing");
            Assert.That(intro.Starts,Is.EqualTo(1));Assert.That(runtime.player,Is.Not.Null);
        }
    }
}
