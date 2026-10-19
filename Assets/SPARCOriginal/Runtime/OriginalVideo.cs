using System;
using UnityEngine;
using UnityEngine.Video;
namespace Sparc.Original
{
    /// <summary>The source movie is served unchanged as streaming media; Unity VideoClip assets cannot play in WebGL.</summary>
    [DefaultExecutionOrder(-31500)] public sealed class OriginalVideo:MonoBehaviour
    {
        public string mediaFile;public bool authoredPlayOnAwake;
        VideoPlayer player;
        void Awake(){player=GetComponent<VideoPlayer>();player.playOnAwake=false;player.source=VideoSource.Url;player.url=Application.streamingAssetsPath+"/OriginalMedia/"+mediaFile;player.errorReceived+=Error;}
        void OnEnable(){if(player&&authoredPlayOnAwake)player.Play();}
        void Start(){if(authoredPlayOnAwake&&!player.isPlaying)player.Play();}
        void Error(VideoPlayer source,string error){if(OriginalRuntime.Instance)OriginalRuntime.Instance.Missing("original-video:"+mediaFile+":"+error);}
        void OnDestroy(){if(player)player.errorReceived-=Error;}
    }
}
