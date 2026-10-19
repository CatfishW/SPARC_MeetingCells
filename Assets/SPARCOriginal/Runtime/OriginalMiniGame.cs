using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Original 120-second falling oxygen game and serialized-duration three-level defense game.</summary>
    public sealed class OriginalMiniGame:MonoBehaviour
    {
        public OriginalBehaviour source;public OriginalActivities owner;
        public bool IsWhite,Finished,Transitioning,Continued;
        public bool Begun {get;private set;}
        RectTransform spawnArea;
        public int Score {get;private set;}public int Level {get;private set;}=1;
        public float Elapsed {get;private set;}public float Duration {get;private set;}
        public RectTransform Area {get;private set;}public GameObject Player {get;private set;}
        public int RemainingHostiles=>tokens.Count(t=>t&&t.Hostile);
        readonly HashSet<OriginalMiniToken> tokens=new();readonly List<GameObject> spawned=new();
        float spawnElapsed=-1,spawnInterval;
        float scoreFeedbackUntil;int scoreFeedback;
        Color scoreInk=Color.white,timeInk=Color.white;
        public OriginalMiniPresentation Visuals {get;private set;}
        public void Begin()
        {
            if(!source||Continued||Begun)return;Begun=true;Clear();Score=0;Level=1;Elapsed=0;Finished=false;Transitioning=false;spawnElapsed=-1;
            IsWhite=source.sourceType=="WhiteBloodCellGameManager";Duration=IsWhite?source.Number("time_limit",60):120;
            spawnArea=source.Component<RectTransform>("oxygenSpawnArea");Player=source.Object("player");
            Area=source.Component<RectTransform>("playArea");if(!Area&&Player)Area=Player.transform.parent as RectTransform;
            if(!Area||!Player){owner.runtime.Missing("original-minigame-references:"+source.sourceId);Finished=true;return;}
            var canvas=source.GetComponentInParent<Canvas>();if(canvas){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=1200;}
            Canvas.ForceUpdateCanvases();var playerSource=Player.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType=="PlayerController2D");
            var driver=OriginalComponents.Ensure<OriginalMiniPlayer>(Player);driver.game=this;driver.source=playerSource;
            foreach(var target in new[]{source.Object("victoryScreen"),source.Object("loadingScreen"),source.Object("GameObject_To_Activate_When_Lost")})if(target)target.SetActive(false);
            if(IsWhite)SpawnLevel();else{var spawn=source.Object("playerSpawnPoint");if(spawn)Player.transform.position=spawn.transform.position;spawnInterval=source.Number("spawnInterval",2);}
            var scoreLabel=source.Component<TMP_Text>("scoreText");if(scoreLabel)scoreInk=scoreLabel.color;
            var timeLabel=source.Component<TMP_Text>("timeText");if(timeLabel)timeInk=timeLabel.color;
            Visuals=canvas?canvas.GetComponent<OriginalMiniPresentation>():null;if(Visuals)Visuals.Bind(this);UpdateLabels();
        }
        void FixedUpdate()
        {
            if(!source||!source.isActiveAndEnabled||Finished||!Area)return;
            Elapsed+=Time.fixedDeltaTime;if(Elapsed>=Duration){End(!IsWhite);return;}
            if(!IsWhite){spawnElapsed+=Time.fixedDeltaTime;if(spawnElapsed>spawnInterval){spawnElapsed=0;float jitter=source.Number("spawnIntervalRange",0.1f);spawnInterval=Mathf.Max(0.01f,source.Number("spawnInterval",2)+UnityEngine.Random.Range(-jitter,jitter));if(tokens.Count<source.Number("maxOxygenOnScreen",25))SpawnOxygen();}}
            UpdateLabels();
        }
        void Label(string field,string value){var label=source.Component<TMP_Text>(field);if(label)label.text=value;}
        void UpdateLabels()
        {
            Label("scoreText","Score: "+Score);if(IsWhite){Label("levelText","Level: "+Level);Label("remainingText","Bacteria Left: "+RemainingHostiles);Label("timeText","Time: "+TimeSpan.FromSeconds(Elapsed).ToString(@"mm\:ss"));}else Label("timeText",TimeSpan.FromSeconds(Mathf.Max(0,Duration-Elapsed)).ToString(@"mm\:ss"));
            // Improve feedback on the original UI without replacing its artwork or rules.
            var score=source.Component<TMP_Text>("scoreText");float pulse=Mathf.Clamp01((scoreFeedbackUntil-Time.unscaledTime)/.65f);
            if(score)score.color=Color.Lerp(scoreInk,scoreFeedback<0?new Color(.72f,.23f,.19f):new Color(.15f,.48f,.28f),pulse);
            var timer=source.Component<TMP_Text>("timeText");if(timer)timer.color=Duration-Elapsed<=15?new Color(.72f,.23f,.19f):timeInk;
        }
        GameObject Spawn(GameObject prefab,Vector2 point)
        {
            if(!prefab)return null;var instance=Instantiate(prefab,Area);spawned.Add(instance);var rect=instance.transform as RectTransform;
            if(rect){rect.localScale=Vector3.one;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f);rect.anchoredPosition=point;}foreach(var b in instance.GetComponentsInChildren<OriginalBehaviour>(true))owner.runtime.Register(b);
            var kind=prefab==source.Object("oxygenPrefab")?"oxygenPrefab":prefab==source.Object("bacteriaPrefab")?"bacteriaPrefab":prefab==source.Object("helpfulCellPrefab")?"helpfulCellPrefab":"obstaclePrefab";
            var skin=GetComponentInParent<Canvas>()?.GetComponent<OriginalMiniPresentation>();if(skin)skin.Token(instance,kind);
            MatchColliders(instance);return instance;
        }
        public static void MatchColliders(GameObject root)
        {
            foreach(var source in root.GetComponentsInChildren<OriginalBehaviour>(true).Where(s=>s.sourceType=="MatchColliderSizeToUI"))
            {var rect=source.transform as RectTransform;if(!rect)continue;var c=source.GetComponent<BoxCollider2D>();if(c){c.size=rect.rect.size;c.offset=rect.rect.center;}var circle=source.GetComponent<CircleCollider2D>();if(circle){circle.radius=Mathf.Min(rect.rect.width,rect.rect.height)*0.5f;circle.offset=rect.rect.center;}}
        }
        void SpawnOxygen()
        {
            var zone=spawnArea?spawnArea:Area;var bounds=zone.rect;var world=zone.TransformPoint(new Vector2(UnityEngine.Random.Range(bounds.xMin,bounds.xMax),UnityEngine.Random.Range(bounds.yMin,bounds.yMax)));var instance=Spawn(source.Object("oxygenPrefab"),Area.InverseTransformPoint(world));
            if(!instance)return;instance.transform.localEulerAngles=new Vector3(0,0,UnityEngine.Random.Range(0f,360));var token=instance.AddComponent<OriginalMiniToken>();token.Oxygen=true;token.game=this;token.source=instance.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType=="OxygenController");tokens.Add(token);
        }
        void SpawnLevel()
        {
            Clear();Canvas.ForceUpdateCanvases();var rect=Area.rect;var used=new List<(Vector2 point,float radius)>();
            Vector2 Pick(float radius)
            {
                Vector2 best=Vector2.zero;float clearance=float.NegativeInfinity;float padding=Mathf.Max(50,radius+8);
                for(int i=0;i<128;i++){var p=new Vector2(UnityEngine.Random.Range(rect.xMin+padding,rect.xMax-padding),UnityEngine.Random.Range(rect.yMin+padding,rect.yMax-padding));float distance=used.Count==0?10000:used.Min(v=>Vector2.Distance(v.point,p)-Mathf.Max(source.Number("crowdingThreshold",100),v.radius+radius+16));if(distance>clearance){best=p;clearance=distance;}if(distance>=0)break;}
                used.Add((best,radius));return best;
            }
            var playerRect=Player.transform as RectTransform;if(playerRect){playerRect.anchoredPosition=new Vector2(rect.xMin+85,rect.yMax-85);used.Add((playerRect.anchoredPosition,Mathf.Max(playerRect.rect.width,playerRect.rect.height)*.5f));}
            void Make(string key,int count,bool character,bool hostile)
            {
                var prefab=source.Object(key);var shape=prefab?prefab.transform as RectTransform:null;float radius=shape?Mathf.Max(shape.rect.width,shape.rect.height)*.5f:50;
                for(int i=0;i<count;i++){var instance=Spawn(prefab,Pick(radius));if(!instance)continue;instance.transform.SetAsFirstSibling();if(character){var token=instance.AddComponent<OriginalMiniToken>();token.game=this;token.Hostile=hostile;token.source=instance.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType=="BacteriaController");tokens.Add(token);}}
            }
            // These exact per-level counts come from the source manager, not the rejected redesign.
            Make("obstaclePrefab",6+Level,false,false);Make("bacteriaPrefab",5,true,true);Make("helpfulCellPrefab",Level,true,false);
        }
        public void Collect(OriginalMiniToken token,int value)
        {if(Finished||Continued||!tokens.Remove(token))return;Score+=value;scoreFeedback=value;scoreFeedbackUntil=Time.unscaledTime+.65f;if(Visuals)Visuals.Collected(token,value);UpdateLabels();if(IsWhite&&token.Hostile&&RemainingHostiles==0)StartCoroutine(NextLevel());}
        public void Remove(OriginalMiniToken token){tokens.Remove(token);}
        IEnumerator NextLevel()
        {
            if(Transitioning||Finished)yield break;Transitioning=true;yield return new WaitForSeconds(0.15f);
            if(Level>=source.Number("maxLevels",3)){Transitioning=false;End(true);yield break;}
            Level++;var screen=source.Object("loadingScreen");if(screen)screen.SetActive(true);
            var loading=screen?screen.GetComponents<OriginalBehaviour>().FirstOrDefault(c=>c.sourceType=="WhiteCellLoadingScreen"):null;
            // The source manager requests oldLevel - 1 before incrementing its level.
            if(loading){var text=loading.Component<TMP_Text>("loadingText");if(text)text.text=OriginalActivities.LoadingTips[Mathf.Clamp(Level-2,0,OriginalActivities.LoadingTips.Length-1)];}
            for(int i=0;i<50;i++){if(loading){var slider=loading.Component<Slider>("loadingBar");if(slider)slider.value=i/49f;}yield return new WaitForSeconds(0.07f);}
            if(screen)screen.SetActive(false);if(!Finished)SpawnLevel();Transitioning=false;
        }
        public void End(bool won)
        {
            if(Finished)return;Finished=true;foreach(var token in tokens)if(token)token.Freeze();var playerBody=Player?Player.GetComponent<Rigidbody2D>():null;if(playerBody)playerBody.linearVelocity=Vector2.zero;
            var screen=source.Object("victoryScreen");if(screen)screen.SetActive(true);var ending=screen?screen.GetComponentsInChildren<OriginalBehaviour>(true).FirstOrDefault(b=>b.sourceType==(IsWhite?"WhiteCellEndGameScreen":"RedBloodCellEndGameScreen")):null;
            if(ending){var score=ending.Component<TMP_Text>("scoreText");if(score)score.text="Final Score: "+Score;var time=ending.Component<TMP_Text>("timeText");if(time)time.text="Time: "+TimeSpan.FromSeconds(Elapsed).ToString(@"mm\:ss");var result=ending.Component<TMP_Text>("resultText");if(result)result.text=Score>=(source.Number("maxLevels",3)*5)-2?OriginalActivities.WhiteGreat:OriginalActivities.WhiteTry;}
            if(IsWhite)
            {
                var loss=source.Object("GameObject_To_Activate_When_Lost");if(loss)loss.SetActive(!won);
                // The original win screen waits for ContinueAfterWin. Only its loss
                // path schedules the authored five-second continuation.
                if(!won)StartCoroutine(ContinueLater(false));
            }
        }
        IEnumerator ContinueLater(bool won){yield return new WaitForSeconds(5);if(!Continued)owner.ContinueMiniGame(this,won);}
        public void Clear(){tokens.Clear();foreach(var g in spawned)if(g)Destroy(g);spawned.Clear();}
        void OnDisable(){if(Player){var body=Player.GetComponent<Rigidbody2D>();if(body)body.linearVelocity=Vector2.zero;}}
    }
}
