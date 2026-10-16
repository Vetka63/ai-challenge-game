using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;
namespace AsciiArena
{
    // Original synthesized cues, no external sound assets. Rate-limited presentation only.
    public sealed class ArenaAudio : MonoBehaviour
    {
        [Inject] private ArenaSimulation simulation;
        [Inject] private ArenaSession session;
        private AudioSource source;
        private AudioClip bolt,pulse,hit,level,finish,confirm,warning,dash;
        private int casts,pulses,kills,levelSeen,serial;
        private bool ended;
        private float previewReady;
        private float previousHurt;
        private int hits,warnings;
        private float hitReady,warningReady,previousDash;
        private void Start()
        {
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;
            bolt=Tone("Rune",700,300,.055f);pulse=Tone("Ward",160,55,.2f);
            hit=Tone("Impact",100,40,.09f);level=Tone("Level",500,1100,.3f);finish=Tone("Result",600,250,.6f);
            confirm=Tone("Contact",1000,400,.035f);warning=Tone("Danger",440,880,.16f);dash=Tone("Dash",350,90,.10f);
            session.VolumePreviewRequested += PreviewVolume;
        }
        private static AudioClip Tone(string name,float from,float to,float duration)
        {
            const int rate=22050;var data=new float[(int)(rate*duration)];float phase=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/data.Length;phase+=Mathf.Lerp(from,to,t)*2*Mathf.PI/rate;
                data[i]=Mathf.Sin(phase)*Mathf.Min(1,t*30)*(1-t)*(1-t)*.22f;
            }
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private void Update()
        {
            if(source==null||simulation==null)return;
            if(serial!=simulation.RunSerial){serial=simulation.RunSerial;casts=pulses=kills=hits=warnings=0;levelSeen=1;ended=false;previousHurt=previousDash=hitReady=warningReady=0;source.Stop();}
            int totalHits=simulation.BoltHits+simulation.PulseHits+simulation.OrbitHits;
            if(simulation.Paused)return;
            if(simulation.Finished&&!ended)source.PlayOneShot(finish,.7f);
            else if(simulation.Level>levelSeen)source.PlayOneShot(level,.7f);
            else if(simulation.HurtFlash>previousHurt+.1f)source.PlayOneShot(hit,.9f);
            else if(simulation.PulseCasts>pulses)source.PlayOneShot(pulse,.5f);
            else if(simulation.Kills>kills)source.PlayOneShot(hit,.25f);
            else if(simulation.BoltCasts>casts)source.PlayOneShot(bolt,.18f);
            if(simulation.DashRemaining>previousDash)source.PlayOneShot(dash,.55f);
            if(simulation.WarningCount>warnings&&Time.unscaledTime>=warningReady){source.PlayOneShot(warning,.22f);warningReady=Time.unscaledTime+.45f;}
            if(totalHits>hits&&Time.unscaledTime>=hitReady){source.PlayOneShot(confirm,.24f);hitReady=Time.unscaledTime+.065f;}
            casts=simulation.BoltCasts;pulses=simulation.PulseCasts;kills=simulation.Kills;levelSeen=simulation.Level;ended=simulation.Finished;previousHurt=simulation.HurtFlash;
            hits=totalHits;warnings=simulation.WarningCount;previousDash=simulation.DashRemaining;
        }
        private void PreviewVolume()
        {
            if (source == null || Time.unscaledTime < previewReady) return;
            source.PlayOneShot(confirm, .5f); previewReady = Time.unscaledTime + .08f;
        }
        private void OnDestroy(){if(session!=null)session.VolumePreviewRequested-=PreviewVolume; foreach(var clip in new[]{bolt,pulse,hit,level,finish,confirm,warning,dash})if(clip!=null)Destroy(clip);}
    }
}
