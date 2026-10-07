using System;

namespace SinmaiAlpha.Preview;

[Serializable]
public sealed class PreviewSettings
{
    public float NoteSpeed=7, TouchSpeed=7, AudioOffsetMs=0, BackgroundDim=.6f;
    public bool Combo=true;
    public float BgmDb=-3, AnswerDb=6, JudgeDb=-10, SlideDb=-10, BreakDb=-10;
    public void Validate()
    {
        static float Range(float v,float min,float max,float fallback) => float.IsNaN(v)||float.IsInfinity(v)?fallback:Math.Max(min,Math.Min(max,v));
        NoteSpeed=Range(NoteSpeed,.1f,20,7);TouchSpeed=Range(TouchSpeed,.1f,20,7);
        AudioOffsetMs=Range(AudioOffsetMs,-500,500,0);BackgroundDim=Range(BackgroundDim,0,1,.6f);
        BgmDb=Range(BgmDb,-80,20,-3);AnswerDb=Range(AnswerDb,-80,20,6);JudgeDb=Range(JudgeDb,-80,20,-10);SlideDb=Range(SlideDb,-80,20,-10);BreakDb=Range(BreakDb,-80,20,-10);
    }
    public static string FilePath => Environment.GetEnvironmentVariable("SINMAI_ALPHA_PREVIEW_SETTINGS") ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MaiChartManager","alpha-preview-settings.json");
}
