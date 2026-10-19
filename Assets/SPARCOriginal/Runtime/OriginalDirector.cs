using System;
using UnityEngine;
using UnityEngine.Playables;

namespace Sparc.Original
{
    /// <summary>Retains the authored playable, bindings, wrap mode and timing. Owns activation once per enable.</summary>
    [DefaultExecutionOrder(-31000)] public sealed class OriginalDirector:MonoBehaviour
    {
        public PlayableDirector director;
        public bool authoredPlayOnAwake;
        public int Starts {get;private set;}
        public int Stops {get;private set;}
        public bool Requested {get;private set;}
        public bool Playing=>director&&director.state==PlayState.Playing;
        bool subscribed;
        void Awake(){if(!director)director=GetComponent<PlayableDirector>();Subscribe();}
        void Subscribe(){if(!director||subscribed)return;director.played+=Played;director.stopped+=Stopped;subscribed=true;}
        void Played(PlayableDirector d){Starts++;}
        void Stopped(PlayableDirector d){Stops++;}
        void OnEnable(){Subscribe();Requested=false;if(OriginalRuntime.Instance&&OriginalRuntime.Instance.Started)Activate();}
        public bool Activate()
        {
            if(!director||!authoredPlayOnAwake||Requested||!isActiveAndEnabled||!director.playableAsset)return false;
            Requested=true;
            // ControlTracks may already own this director. Never start a second graph over them.
            if(director.state!=PlayState.Playing){director.time=director.initialTime;director.Play();}
            return true;
        }
        void OnDisable(){Requested=false;}
        void OnDestroy(){if(director&&subscribed){director.played-=Played;director.stopped-=Stopped;}subscribed=false;}
    }
}
