using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sparc.Original
{
    /// <summary>Original activities use original canvases, pages, prefabs and serialized durations.</summary>
    public sealed class OriginalActivities:MonoBehaviour
    {
        public OriginalRuntime runtime;
        public static readonly string[] LoadingTips={"White blood cells are responsible for protecting the body from invaders like bacteria.","There are different types of white blood cells, each with different jobs.","White blood cells are so big, they have a nucleus and DNA, unlike red blood cells.","White blood cells have unique cell membranes that let them be strong and flexible, squeezing into small spaces."};
        public const string WhiteGreat="Congratulations!\r\n\r\nYou're a great white blood cell! You didn't hit any red blood cells!";
        public const string WhiteTry="Nice try!\r\n\r\nYou might've hit some red blood cells, or let some bacteria get away. Remember to watch out for those red blood cells!";
        readonly Dictionary<int,OriginalMiniGame> games=new();
        readonly Dictionary<GameObject,bool> hiddenHud=new();
        readonly HashSet<int> preparedWords=new();
        public bool IsWorksheetOpen=>runtime&&runtime.OfType("WordGameManager").Any(c=>c.isActiveAndEnabled);
        public bool IsOpen=>games.Values.Any(g=>g&&g.source&&g.source.isActiveAndEnabled&&!g.Continued);
        public IEnumerable<OriginalMiniGame> Games=>games.Values;
        readonly Dictionary<int,int> wordPages=new();
        readonly HashSet<int> submitted=new();
        public int WordCompletionBroadcasts {get;private set;}
        public readonly List<string> LastSubmission=new();
        public void Activated(OriginalBehaviour c)
        {
            if(c.sourceType=="WordGameManager")
            {wordPages.TryAdd(c.GetInstanceID(),0);PrepareWords(c);ShowPage(c,wordPages[c.GetInstanceID()]);}
            else if(c.sourceType=="OnSubmitSceneTransition")
            {var hidden=c.Object("object2disable");if(hidden)runtime.SetActive(hidden,false);}
            else if(c.sourceType is "WhiteBloodCellGameManager" or "RedBloodCellGameManager")
            {
                int id=c.GetInstanceID();if(!games.TryGetValue(id,out var game)){game=c.gameObject.AddComponent<OriginalMiniGame>();game.source=c;game.owner=this;games[id]=game;}
                if(!game.Begun){runtime.activityUi.PrepareGame(c,true);SuppressHud();game.Begin();}
            }
            else if(c.sourceType=="MatchColliderSizeToUI")OriginalMiniGame.MatchColliders(c.gameObject);
            else if(c.sourceType=="ExitConfirmation")ExitPanel(c,false);
        }
        void PrepareWords(OriginalBehaviour manager)
        {
            if(!preparedWords.Add(manager.GetInstanceID()))return;
            foreach(var page in Pages(manager))foreach(var c in page.GetComponentsInChildren<OriginalBehaviour>(true))
            {if(c.sourceType=="DraggableUIElement"){var tile=c.GetComponent<OriginalWordTile>()??c.gameObject.AddComponent<OriginalWordTile>();tile.duration=Mathf.Clamp(c.Number("animationDuration",0.5f),.10f,.18f);}else if(c.sourceType=="DropSlotUIElement"&&!c.GetComponent<OriginalWordSlot>())c.gameObject.AddComponent<OriginalWordSlot>();}
        }
        void SuppressHud()
        {
            string[] names={"GameHUD","HUD","PlayerCanvas","ObjectiveCanvas","ChatCanvas","MqttChatCanvas","DynamicInteractionUI","DynamicInteractionUIForDriving","HUD_Command","MobileControlCanvas"};
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(names.Contains(t.name)&&!hiddenHud.ContainsKey(t.gameObject)){hiddenHud[t.gameObject]=t.gameObject.activeSelf;t.gameObject.SetActive(false);}
        }
        void RestoreHud(){foreach(var pair in hiddenHud)if(pair.Key)pair.Key.SetActive(pair.Value);hiddenHud.Clear();}
        public void ContinueMiniGame(OriginalMiniGame game,bool won)
        {
            if(!game||game.Continued)return;game.Continued=true;game.Finished=true;game.Clear();
            var canvas=game.GetComponentInParent<Canvas>();if(canvas)canvas.gameObject.SetActive(false);else game.gameObject.SetActive(false);RestoreHud();
            // These are the original bridge's exact continuation blocks.
            runtime.story.Execute(game.IsWhite?(won?"WBC_WIN":"WBC_LOSE"):"AfterRBCMinigame");
        }
        public bool IsExitPromptVisible(OriginalBehaviour source)
        {
            var panel=source?source.Object("exitConfirmationPanel"):null;
            if(!panel||!panel.activeInHierarchy)return false;
            var group=panel.GetComponent<CanvasGroup>();return !group||(group.alpha>0.5f&&group.interactable&&group.blocksRaycasts);
        }
        void ExitPanel(OriginalBehaviour source,bool visible)
        {
            var panel=source.Object("exitConfirmationPanel");if(!panel)return;
            if(panel==source.gameObject){var group=OriginalComponents.Ensure<CanvasGroup>(panel);group.alpha=visible?1:0;group.interactable=visible;group.blocksRaycasts=visible;}
            else panel.SetActive(visible);
        }
        void Update()
        {
            if(!runtime.Started||!IsOpen)return;var key=UnityEngine.InputSystem.Keyboard.current;if(key==null)return;
            var exit=runtime.OfType("ExitConfirmation").FirstOrDefault(c=>c.isActiveAndEnabled);if(!exit)return;
            if(key.escapeKey.wasPressedThisFrame){ExitPanel(exit,!IsExitPromptVisible(exit));}
            if(key.enterKey.wasPressedThisFrame&&IsExitPromptVisible(exit))Call(exit,"OnConfirmButton",null);
        }
        GameObject[] Pages(OriginalBehaviour c)=>(c.Data["wordGamePages"] as JArray??new JArray()).Select(r=>OriginalBehaviour.AsGameObject(c.Resolve(r))).Where(g=>g).ToArray();
        void ShowPage(OriginalBehaviour c,int index)
        {
            var pages=Pages(c);for(int i=0;i<pages.Length;i++)runtime.SetActive(pages[i],i==index);
            var label=c.Component<TMP_Text>("nextButtonText");if(label)label.text=index==pages.Length-1?"Submit":"Next";
        }
        public bool Call(OriginalBehaviour c,string method,object arg)
        {
            if(c.sourceType=="clicked"&&method=="OnReturnButtonClicked"){c.gameObject.SetActive(false);return true;}
            if(c.sourceType=="ExitConfirmation")
            {
                if(method=="OnCancelButton"){ExitPanel(c,false);return true;}
                if(method=="OnExitButton"){ExitPanel(c,true);return true;}
                if(method=="OnConfirmButton"){if(!IsExitPromptVisible(c))return true;ExitPanel(c,false);var active=games.Values.FirstOrDefault(g=>g&&g.source.isActiveAndEnabled&&!g.Continued);if(active)ContinueMiniGame(active,false);return true;}
            }
            if(c.sourceType=="WhiteCellEndGameScreen"&&method is "ContinueAfterWin" or "ContinueAfterLoss")
            {var game=games.Values.FirstOrDefault(g=>g&&g.IsWhite);if(game)ContinueMiniGame(game,method=="ContinueAfterWin");return true;}

            if(c.sourceType=="WordGameManager"&&method is "NextPage" or "PrevPage" or "Submit")
            {
                int id=c.GetInstanceID();if(submitted.Contains(id))return true;var pages=Pages(c);int current=wordPages.TryGetValue(id,out var p)?p:0;
                if(method=="PrevPage"){current=Mathf.Max(0,current-1);wordPages[id]=current;ShowPage(c,current);return true;}
                if(method=="NextPage"&&current+1<pages.Length){wordPages[id]=current+1;ShowPage(c,current+1);return true;}
                // Exactly one final broadcast. The original NextPage -> OnSubmit path sent it twice.
                if(!submitted.Add(id))return true;LastSubmission.Clear();
                foreach(var page in pages)
                {foreach(var slot in page.GetComponentsInChildren<OriginalWordSlot>(true))LastSubmission.Add(slot.Current?slot.Current.Word:"");foreach(var field in page.GetComponentsInChildren<TMP_InputField>(true))LastSubmission.Add(field.text);}
                PlayerPrefs.SetString("SPARC.Original.word-submission",Newtonsoft.Json.JsonConvert.SerializeObject(LastSubmission));PlayerPrefs.Save();
                WordCompletionBroadcasts++;
                foreach(var listener in runtime.OfType("OnWordGameEndEvents").Where(x=>x.isActiveAndEnabled).ToArray())runtime.Events(listener,"onWordGameEndEvent");
                foreach(var listener in runtime.OfType("OnSubmitSceneTransition").Where(x=>x.isActiveAndEnabled).ToArray())
                {string target=listener.Text("SceneToLoad");if(!string.IsNullOrWhiteSpace(target)){if(Application.CanStreamedLevelBeLoaded(target))SceneManager.LoadSceneAsync(target);else runtime.Missing("required-original-scene:"+target);}}
                return true;
            }
            if(c.sourceType=="RBCQuestion"&&method=="CompleteQuestion")
            {runtime.objectives.Answered();return true;}
            if(c.sourceType=="ObjectiveAnswerQuestions"&&method=="CompleteQuestion"){runtime.objectives.Answered();return true;}
            if(c.sourceType=="LoadHeart"&&method=="OnStartButtonClick_ToHeart")
            {string target=c.Text("SceneToLoad","Heart");if(Application.CanStreamedLevelBeLoaded(target))SceneManager.LoadSceneAsync(target);else runtime.Missing("required-original-scene:"+target);return true;}
            return false;
        }
    }
}
