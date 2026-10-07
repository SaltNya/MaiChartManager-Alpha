using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SinmaiAlpha.Preview;

public sealed class PreviewGain : MonoBehaviour
{
    public volatile float Gain=1;
    void OnAudioFilterRead(float[] data,int channels){var gain=Gain;for(var i=0;i<data.Length;i++)data[i]*=gain;}
}

public sealed class PreviewAudio : MonoBehaviour
{
    static PreviewAudio instance;
    AudioSource source;
    PreviewGain gain;
    AudioTimeProvider clock;
    AudioClip clip;
    PreviewControls controls;
    bool reported;
    float previous=float.NaN;
    int cursor;
    readonly Dictionary<string,AudioClip> sounds=new Dictionary<string,AudioClip>();
    readonly Dictionary<string,AudioSource> channels=new Dictionary<string,AudioSource>();
    readonly Dictionary<string,PreviewGain> gains=new Dictionary<string,PreviewGain>();
    readonly List<SoundEvent> events=new List<SoundEvent>();
    public float Length=>clip?.length??0;
    struct SoundEvent { public double time;public string sound,group; }
    [Serializable] class Chart {public Timing[] timingList;}
    [Serializable] class Timing {public double time;public Note[] noteList;}
    [Serializable] class Note {public int noteType;public double holdTime,slideStartTime,slideTime;public bool isBreak,isEx,isMineHead,isMineSlide,isFake,isFakeHead,isFakeSlide,isSlideNoHead,isSlideBreak,isHanabi;}
    public static void Initialize()
    {
        if(instance!=null)return;
        var host=new GameObject("Sinmai-Alpha preview");DontDestroyOnLoad(host);
        instance=host.AddComponent<PreviewAudio>();host.AddComponent<PreviewControls>();
    }
    public static AudioClip ReadWave(string path)
    {
        using var reader=new BinaryReader(File.OpenRead(path));
        if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="RIFF")throw new InvalidDataException("Expected RIFF WAV");
        reader.ReadInt32();if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="WAVE")throw new InvalidDataException("Expected WAVE");
        int count=0,rate=0,bits=0,format=0;byte[] pcm=null;
        while(reader.BaseStream.Position+8<=reader.BaseStream.Length)
        {
            var id=Encoding.ASCII.GetString(reader.ReadBytes(4));var length=reader.ReadInt32();
            if(length<0||reader.BaseStream.Position+length>reader.BaseStream.Length)throw new InvalidDataException("Invalid WAV chunk");
            var end=reader.BaseStream.Position+length;
            if(id=="fmt "){format=reader.ReadInt16();count=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();}
            else if(id=="data")pcm=reader.ReadBytes(length);
            reader.BaseStream.Position=end+(length&1);
        }
        if(pcm==null||format!=1||bits!=16||count<1||rate<8000)throw new InvalidDataException("Preview requires PCM16 WAV");
        var samples=new float[pcm.Length/2];for(var i=0;i<samples.Length;i++)samples[i]=(short)(pcm[i*2]|pcm[i*2+1]<<8)/32768f;
        var result=AudioClip.Create(Path.GetFileNameWithoutExtension(path),samples.Length/count,count,rate,false);result.SetData(samples,0);return result;
    }
    void Awake()
    {
        source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;gain=gameObject.AddComponent<PreviewGain>();
        var path=Environment.GetEnvironmentVariable("SINMAI_ALPHA_PREVIEW_WAV");if(string.IsNullOrEmpty(path))return;
    }
    System.Collections.IEnumerator Start()
    {
        var path=Environment.GetEnvironmentVariable("SINMAI_ALPHA_PREVIEW_WAV");
        if(string.IsNullOrEmpty(path))yield break;
        while(!File.Exists(Path.Combine(Path.GetDirectoryName(path),"resources-ready.txt")))yield return null;
        // The window and its loading UI have rendered before decoding assets.
        yield return null;
        try
        {
            clip=ReadWave(path);source.clip=clip;
            var sfx=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","SFX"));
            foreach(var name in new[]{"answer","judge","judge_ex","slide","break","judge_break","touch","hanabi"})
                sounds[name]=ReadWave(Path.Combine(sfx,name+".wav"));
            foreach(var group in new[]{"Answer","Judge","Slide","Break"})
            {
                var child=new GameObject("Preview "+group);child.transform.SetParent(transform);
                var channel=child.AddComponent<AudioSource>();channel.playOnAwake=false;channel.spatialBlend=0;
                channels[group]=channel;gains[group]=child.AddComponent<PreviewGain>();
            }
            var chart=Newtonsoft.Json.JsonConvert.DeserializeObject<Chart>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(path),"chart.json")));
            void Add(double time,string sound,string group)=>events.Add(new SoundEvent{time=time,sound=sound,group=group});
            foreach(var timing in chart.timingList??Array.Empty<Timing>())foreach(var n in timing.noteList??Array.Empty<Note>())
            {
                if(n.isFake)continue;
                var slide=n.noteType==1;
                // 噪域没有得分或判定音效；无头滑条也不能重复播放星头音效。
                if(n.noteType>4)continue;
                if(!n.isMineHead&&!n.isFakeHead&&(!slide||!n.isSlideNoHead))
                {
                    Add(timing.time,"answer","Answer");
                    Add(timing.time,n.isBreak?"judge_break":n.isEx?"judge_ex":n.noteType==3||n.noteType==4?"touch":"judge",n.isBreak?"Break":"Judge");
                    if(n.isHanabi)Add(timing.time,"hanabi","Judge");
                }
                if(slide&&!n.isMineSlide&&!n.isFakeSlide)
                {
                    Add(n.slideStartTime,"slide","Slide");
                    if(n.isSlideBreak)Add(n.slideStartTime+n.slideTime,"break","Break");
                }
            }
            var ordered=events.GroupBy(e=>(Math.Round(e.time,5),e.sound,e.group)).Select(g=>g.First()).OrderBy(e=>e.time).ToArray();events.Clear();events.AddRange(ordered);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"audio-ready.txt"),"PCM16 and sound effects loaded");
            Debug.Log("[Sinmai-Alpha Preview] Prepared "+events.Count+" sound events");
        }
        catch(Exception e){File.WriteAllText(Path.Combine(Path.GetDirectoryName(path),"audio-error.txt"),e.Message);Debug.LogError("[Sinmai-Alpha Preview] Audio failed: "+e);}
    }
    public void ResetSounds(){foreach(var channel in channels.Values)channel.Stop();previous=float.NaN;}
    static float Db(float value)=>value<=-80?0:Mathf.Pow(10,value/20);
    void LateUpdate()
    {
        if(clip==null)return;
        if(controls==null)controls=GetComponent<PreviewControls>();
        var settings=controls?.Values;if(settings!=null)
        {
            gain.Gain=Db(settings.BgmDb);
            if(gains.Count==4){gains["Answer"].Gain=Db(settings.AnswerDb);gains["Judge"].Gain=Db(settings.JudgeDb);gains["Slide"].Gain=Db(settings.SlideDb);gains["Break"].Gain=Db(settings.BreakDb);}
        }
        if(clock==null){var obj=GameObject.Find("AudioTimeProvider");if(obj!=null)clock=obj.GetComponent<AudioTimeProvider>();}
        if(clock==null)return;
        var t=clock.AudioTime;
        if(!clock.PlaybackStarted||clock.IsPaused||clock.IsPreview){if(source.isPlaying)source.Pause();ResetSounds();return;}
        var musicTime=t-(settings?.AudioOffsetMs??0)/1000;
        if(musicTime>=0&&musicTime<clip.length)
        {
            source.pitch=clock.CurrentSpeed;
            if(!source.isPlaying||Math.Abs(source.time-musicTime)>.05f)source.timeSamples=Math.Min(clip.samples-1,(int)(musicTime*clip.frequency));
            if(!source.isPlaying)source.Play();
            if(!reported){reported=true;Debug.Log("[Sinmai-Alpha Preview] Audio playing, time="+t+", samples="+clip.samples);}
        }
        else if(source.isPlaying)source.Pause();
        if(float.IsNaN(previous)||t<previous||t-previous>.5f)
        {
            cursor=0;while(cursor<events.Count&&events[cursor].time<t-.001)cursor++;
        }
        while(cursor<events.Count&&events[cursor].time<=t)
        {
            var e=events[cursor++];var channel=channels[e.group];channel.pitch=clock.CurrentSpeed;channel.PlayOneShot(sounds[e.sound]);
        }
        previous=t;
    }
    void OnDestroy(){if(clip!=null)Destroy(clip);foreach(var sound in sounds.Values)Destroy(sound);instance=null;}
}
