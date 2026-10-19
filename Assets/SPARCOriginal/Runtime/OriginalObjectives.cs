using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Original objective graph, objects, text, effects and UI prefabs. No replacement objectives.</summary>
    public sealed class OriginalObjectives:MonoBehaviour
    {
        public OriginalRuntime runtime;
        readonly HashSet<int> completed=new();readonly Dictionary<int,int> activation=new();
        readonly Dictionary<int,GameObject> rows=new();int sequence;float nextScan;
        public int CompletedCount=>completed.Count;
        static bool Objective(OriginalBehaviour b)=>b&&b.sourceType is "ObjectiveReachPoint" or "ObjectiveTalkWithNPC" or "ObjectiveAnswerQuestions";
        public OriginalBehaviour ActiveTalk=>runtime.components.Where(b=>Objective(b)&&b.sourceType=="ObjectiveTalkWithNPC"&&b.isActiveAndEnabled&&!completed.Contains(b.GetInstanceID())).OrderByDescending(b=>activation.TryGetValue(b.GetInstanceID(),out var s)?s:0).FirstOrDefault();
        public void Activated(OriginalBehaviour b)
        {
            if(!Objective(b)||completed.Contains(b.GetInstanceID()))return;activation[b.GetInstanceID()]=++sequence;
            var effect=b.Object("QuestEffectAboveHead");if(effect)effect.SetActive(true);RegisterRow(b);
        }
        public void Talked(OriginalBehaviour npc)
        {foreach(var objective in runtime.components.Where(b=>b&&b.sourceType=="ObjectiveTalkWithNPC"&&b.isActiveAndEnabled&&b.Object("NPCTalkTo")==npc.gameObject).ToArray())Complete(objective,true);}
        public void Answered()
        {foreach(var objective in runtime.components.Where(b=>b&&b.sourceType=="ObjectiveAnswerQuestions"&&b.isActiveAndEnabled).ToArray())Complete(objective,true);}
        public void Reached(OriginalBehaviour objective)
        {
            if(!objective||objective.sourceType!="ObjectiveReachPoint"||completed.Contains(objective.GetInstanceID()))return;Complete(objective,false);
            if(objective.Flag("ActivateObjects"))foreach(var reference in objective.Data["ObjectsToActivate"] as JArray??new JArray()){var target=OriginalBehaviour.AsGameObject(objective.Resolve(reference));if(target)runtime.SetActive(target,true);}
            var roots=objective.Data["DestroyRoots"] as JArray;
            if(roots==null||roots.Count==0)Destroy(objective.gameObject);else foreach(var reference in roots){var target=OriginalBehaviour.AsGameObject(objective.Resolve(reference));if(target)Destroy(target);}
        }
        void Complete(OriginalBehaviour b,bool destroy)
        {
            if(!completed.Add(b.GetInstanceID()))return;runtime.EventTrace.Add("objective-complete:"+b.Text("Title",b.Text("Question")));
            var next=b.Object("NextObjective");if(next)runtime.SetActive(next,true);var effect=b.Object("QuestEffectAboveHead");if(effect)effect.SetActive(false);
            if(rows.TryGetValue(b.GetInstanceID(),out var row)){if(row)StartCoroutine(RemoveRow(row));rows.Remove(b.GetInstanceID());}
            if(destroy)Destroy(b.gameObject);
        }
        void RegisterRow(OriginalBehaviour objective)
        {
            int id=objective.GetInstanceID();if(rows.ContainsKey(id)||completed.Contains(id))return;
            var manager=runtime.First("ObjectiveHUDManager");if(!manager)return;var panel=manager.Component<RectTransform>("ObjectivePanel");var prefab=manager.Object(objective.Flag("IsOptional")?"SecondaryObjectivePrefab":"PrimaryObjectivePrefab");
            if(!panel||!prefab){runtime.Missing("original-objective-hud-references");return;}
            panel.gameObject.SetActive(true);var instance=Instantiate(prefab,panel);instance.SetActive(true);instance.transform.localScale=Vector3.one;
            if(!objective.Flag("IsOptional"))instance.transform.SetAsFirstSibling();rows[id]=instance;
            foreach(var proxy in instance.GetComponentsInChildren<OriginalBehaviour>(true))runtime.Register(proxy);
            var toast=instance.GetComponents<OriginalBehaviour>().FirstOrDefault(b=>b.sourceType=="ObjectiveToast");if(!toast){runtime.Missing("original-objective-toast");return;}
            string title=objective.Text("Title",objective.Text("Question"));if(string.IsNullOrEmpty(title))title=objective.Text("Question");
            string description=objective.Text("Description",objective.Text("description"));
            var t=toast.Component<TMP_Text>("TitleTextContent");if(t)t.text=title;var d=toast.Component<TMP_Text>("DescriptionTextContent");if(d)d.text=description;var count=toast.Component<TMP_Text>("CounterTextContent");if(count)count.text="";
            var sub=toast.Object("SubTitleRect");if(sub)sub.SetActive(!string.IsNullOrEmpty(description));if(runtime.hud)runtime.hud.RefreshPresentation();StartCoroutine(ShowRow(toast,Mathf.Max(0,objective.Number("DelayVisible"))));
        }
        static float Curve(JToken data,float time)
        {
            var keys=data?["m_Curve"] as JArray;if(keys==null||keys.Count==0)return 0;float left=(float?)keys[0]["value"]??0;
            for(int i=1;i<keys.Count;i++){float end=(float?)keys[i]["time"]??1;float begin=(float?)keys[i-1]["time"]??0;float right=(float?)keys[i]["value"]??0;if(time<=end)return Mathf.Lerp(left,right,Mathf.InverseLerp(begin,end,time));left=right;}return left;
        }
        IEnumerator ShowRow(OriginalBehaviour toast,float delay)
        {
            var group=toast.Component<CanvasGroup>("CanvasGroup")??toast.GetComponent<CanvasGroup>();if(group)group.alpha=0;if(delay>0)yield return new WaitForSeconds(delay);
            float duration=Mathf.Max(0.001f,Mathf.Max(toast.Number("FadeInDuration",0.5f),toast.Number("MoveInDuration",0.5f)));float elapsed=0;var layout=toast.Component<HorizontalOrVerticalLayoutGroup>("LayoutGroup");
            while(toast&&elapsed<duration){elapsed+=Time.deltaTime;if(group)group.alpha=Mathf.Clamp01(elapsed/Mathf.Max(0.001f,toast.Number("FadeInDuration",0.5f)));if(layout){layout.padding.left=(int)Curve(toast.Data["MoveInCurve"],Mathf.Clamp01(elapsed/Mathf.Max(0.001f,toast.Number("MoveInDuration",0.5f))));LayoutRebuilder.MarkLayoutForRebuild((RectTransform)layout.transform);}yield return null;}
            if(layout)layout.padding.left=0;if(group)group.alpha=1;
        }
        IEnumerator RemoveRow(GameObject row)
        {
            var toast=row.GetComponents<OriginalBehaviour>().FirstOrDefault(b=>b.sourceType=="ObjectiveToast");if(!toast){Destroy(row);yield break;}
            yield return new WaitForSeconds(toast.Number("CompletionDelay"));var group=toast.Component<CanvasGroup>("CanvasGroup");float duration=Mathf.Max(0.001f,toast.Number("FadeOutDuration",2));float time=0;
            while(row&&time<duration){time+=Time.deltaTime;if(group)group.alpha=1-Mathf.Clamp01(time/duration);yield return null;}if(row)Destroy(row);
        }
        void Update()
        {
            if(!runtime||!runtime.Started)return;var camera=Camera.main;
            foreach(var objective in runtime.components.Where(b=>Objective(b)&&b.isActiveAndEnabled&&!completed.Contains(b.GetInstanceID())))
            {var effect=objective.Object("QuestEffectAboveHead");if(effect&&camera)effect.transform.LookAt(camera.transform.position);}
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+0.5f;
            foreach(var objective in runtime.components.Where(b=>Objective(b)&&b.isActiveAndEnabled&&!completed.Contains(b.GetInstanceID())).ToArray())RegisterRow(objective);
        }
    }
}
