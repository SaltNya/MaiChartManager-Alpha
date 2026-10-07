using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MaiChartManager.Models;
using MaiChartManager.Utils;

namespace MaiChartManager.Services;

public record AlphaPreviewOptions(double NoteSpeed = 7, double TouchSpeed = 7, double StartTime = 0);
public record AlphaPreviewSession(string Session, bool Playing, string Message);

// 一次只拥有一个播放器。仅退出本服务创建的进程，不操作用户制谱器。
public sealed class AlphaPreviewService : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Process? player;
    private AlphaPreviewWindow? window;
    private string? session;
    private string? work;
    private string? endpoint;
    private string? jsonPath;
    private AlphaPreviewOptions options = new();
    private SinmaiAlpha.Preview.PreviewSettings display = new();
    private readonly HttpClient http = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromMinutes(2) };
    public async Task<AlphaPreviewSession> Start(MusicXmlWithABJacket music, int level, string? side, AlphaPreviewOptions request, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException(AlphaText.Get("AlphaWindowsOnly"));
        if (!SinmaiAlphaIntegration.Status().PreviewReady) throw new InvalidOperationException(AlphaText.Get("AlphaPreviewMissing"));
        if (!double.IsFinite(request.NoteSpeed) || request.NoteSpeed is < 0 or > 20 || !double.IsFinite(request.TouchSpeed) || request.TouchSpeed is < 0 or > 20 || !double.IsFinite(request.StartTime) || request.StartTime < 0) throw new ArgumentException(AlphaText.Get("AlphaPreviewInvalid"));
        var path = SinmaiAlphaIntegration.ChartPath(music, level, side);
        if (!File.Exists(path)) throw new FileNotFoundException(AlphaText.Get("AlphaChartMissing"));
        await gate.WaitAsync(cancellation);
        try
        {
            StopOwned(); options = request;
            // 在任何谱面读取、音频转换或 Unity 启动之前显示窗口。
            window=await AlphaPreviewWindow.Open(Path.Combine(SinmaiAlphaIntegration.PreviewRoot,"UI"));
            using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation,window.ClosedToken.Token);
            cancellation=lifetime.Token;
            display = new SinmaiAlpha.Preview.PreviewSettings();
            try {
                if (File.Exists(SinmaiAlpha.Preview.PreviewSettings.FilePath))
                    display = JsonSerializer.Deserialize<SinmaiAlpha.Preview.PreviewSettings>(File.ReadAllText(SinmaiAlpha.Preview.PreviewSettings.FilePath),new JsonSerializerOptions { IncludeFields=true }) ?? display;
            } catch (Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) { }
            display.Validate();
            options = options with { NoteSpeed=display.NoteSpeed, TouchSpeed=display.TouchSpeed };
            session = Guid.NewGuid().ToString("N");
            work = Path.Combine(StaticSettings.tempPath, "AlphaPreview", session);
            Directory.CreateDirectory(work);
            await Stage(1,cancellation);
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            endpoint=$"http://127.0.0.1:{port}/{session}/";
            var previewRoot = SinmaiAlphaIntegration.PreviewRoot;
            var launch = new ProcessStartInfo(Path.Combine(previewRoot, "Viewer", "MajdataView.exe")) {
                WorkingDirectory=Path.Combine(previewRoot,"Viewer"), UseShellExecute=false, CreateNoWindow=true
            };
            foreach(var arg in new[]{"-screen-fullscreen","0","-screen-width","960","-screen-height","640","-logFile",Path.Combine(work,"player.log")}) launch.ArgumentList.Add(arg);
            foreach(var arg in new[]{"-parentHWND",window.HostHandle.ToInt64().ToString(),"delayed"})launch.ArgumentList.Add(arg);
            launch.Environment["SINMAI_ALPHA_PREVIEW_HOST"]=window.HostHandle.ToInt64().ToString();
            launch.Environment["SINMAI_ALPHA_PREVIEW_URL"]=endpoint;
            launch.Environment["SINMAI_ALPHA_PREVIEW_WAV"]=Path.Combine(work,"track.wav");
            player=Process.Start(launch) ?? throw new IOException(AlphaText.Get("AlphaPlayerStartFailed"));
            window.Watch(player);
            // 等待播放器完成初始化；加载遮罩保持到后续 frame-ready 首帧信号。
            var timeout=Stopwatch.StartNew();
            while(true)
            {
                cancellation.ThrowIfCancellationRequested();
                player.Refresh();
                if(player.HasExited) throw new InvalidOperationException(AlphaText.Get("AlphaPlayerExited"));
                if(timeout.Elapsed.TotalSeconds>40) throw new TimeoutException(AlphaText.Get("AlphaPlayerStartTimeout"));
                if(File.Exists(Path.Combine(work,"audio-error.txt"))) throw new InvalidOperationException(AlphaText.Get("AlphaAudioLoadFailed") + await File.ReadAllTextAsync(Path.Combine(work,"audio-error.txt"),cancellation));
                if(File.Exists(Path.Combine(work,"ui-error.txt"))) throw new InvalidOperationException(AlphaText.Get("AlphaUiLoadFailed") + await File.ReadAllTextAsync(Path.Combine(work,"ui-error.txt"),cancellation));
                if(File.Exists(Path.Combine(work,"player-ready.txt"))) break;
                await Task.Delay(150,cancellation);
            }
            await window.Attach(player);
            await Stage(2,cancellation);
            var source = SinmaiAlphaIntegration.ReadSource(path) ?? new AlphaPreviewSource(1, SinmaiAlphaIntegration.Hash(path), "", 0, music.Name, music.Artist, "", "", "4", await File.ReadAllTextAsync(path, cancellation));
            source = source with { Ma2 = source.Ma2 ?? await File.ReadAllTextAsync(path, cancellation) };
            var sourcePath = Path.Combine(work, "source.json");
            jsonPath = Path.Combine(work, "chart.json");
            await Stage(3,cancellation);
            await Task.Run(()=>CopyChartAssets(Path.GetDirectoryName(path)!, work,cancellation),cancellation);
            var previewSource = JsonSerializer.SerializeToNode(source)!;
            previewSource["PreviewDefaults"] = JsonSerializer.SerializeToNode(display,new JsonSerializerOptions{IncludeFields=true});
            await File.WriteAllTextAsync(sourcePath, previewSource.ToJsonString(), cancellation);
            var audio = await AudioConvert.GetCachedWavPath(music.CueId, music.Id) ?? throw new FileNotFoundException(AlphaText.Get("AlphaAudioMissing"));
            Directory.CreateDirectory(work);
            File.Copy(audio, Path.Combine(work, "track.wav"), true);
            var jacket = ImageConvert.GetMusicJacketPngData(music);
            if (jacket != null && !File.Exists(Path.Combine(work,"bg.png"))) await File.WriteAllBytesAsync(Path.Combine(work, "bg.png"), jacket, cancellation);
            await Stage(4,cancellation);
            var bridge = new ProcessStartInfo(Path.Combine(previewRoot, "Bridge", "SinmaiAlpha.PreviewBridge.exe")) {
                UseShellExecute=false, CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden,
                RedirectStandardOutput=true, RedirectStandardError=true
            };
            bridge.ArgumentList.Add(Path.Combine(previewRoot, "Parser")); bridge.ArgumentList.Add(sourcePath); bridge.ArgumentList.Add(jsonPath);
            using (var proc = Process.Start(bridge) ?? throw new IOException(AlphaText.Get("AlphaParserStartFailed")))
            {
                var output=proc.StandardOutput.ReadToEndAsync(cancellation); var error=proc.StandardError.ReadToEndAsync(cancellation);
                try { await proc.WaitForExitAsync(cancellation); }
                catch { if(!proc.HasExited) proc.Kill(true); throw; }
                await output; var errorText=await error;
                if(proc.ExitCode!=0) throw new InvalidOperationException(AlphaText.Get("AlphaParseFailed")+errorText);
            }
            await Stage(5,cancellation);
            await File.WriteAllTextAsync(Path.Combine(work,"resources-ready.txt"),"ready",cancellation);
            var audioTimeout=Stopwatch.StartNew();
            while(!File.Exists(Path.Combine(work,"audio-ready.txt"))) {
                cancellation.ThrowIfCancellationRequested();
                if(player.HasExited) throw new InvalidOperationException(AlphaText.Get("AlphaWindowClosed"));
                if(File.Exists(Path.Combine(work,"audio-error.txt"))) throw new InvalidOperationException(await File.ReadAllTextAsync(Path.Combine(work,"audio-error.txt"),cancellation));
                if(audioTimeout.Elapsed.TotalSeconds>40) throw new TimeoutException(AlphaText.Get("AlphaAudioTimeout"));
                await Task.Delay(100,cancellation);
            }
            await Stage(6,cancellation);
            await Send(0,request.StartTime,cancellation);
            await Send(8,request.StartTime,cancellation);
            await Stage(7,cancellation);
            await File.WriteAllTextAsync(Path.Combine(work,"chart-ready.txt"),"paused",cancellation);
            var frameTimeout=Stopwatch.StartNew();
            while(!File.Exists(Path.Combine(work,"frame-ready.txt"))) {
                cancellation.ThrowIfCancellationRequested();
                if(player.HasExited)throw new InvalidOperationException(AlphaText.Get("AlphaWindowClosed"));
                if(frameTimeout.Elapsed.TotalSeconds>20)throw new TimeoutException(AlphaText.Get("AlphaFrameTimeout"));
                await Task.Delay(50,cancellation);
            }
            window.RevealPlayer();
            var openedSession=session;
            window.ClosedToken.Token.Register(()=> { _=Task.Run(()=>Stop(openedSession)); });
            return new(session,true,AlphaText.Get("AlphaPreviewActiveHint"));
        }
        catch (Exception error) { Directory.CreateDirectory(Path.Combine(StaticSettings.tempPath,"AlphaPreview")); File.WriteAllText(Path.Combine(StaticSettings.tempPath,"AlphaPreview","last-error.txt"), error.ToString()); StopOwned(); throw; }
        finally { gate.Release(); }
    }
    public async Task<AlphaPreviewSession> Stop(string id)
    {
        await gate.WaitAsync();
        try { if(id==session) StopOwned(); return new(id,false,AlphaText.Get("AlphaPreviewClosed")); }
        finally { gate.Release(); }
    }
    public AlphaPreviewSession State(string id) => new(id,id==session && player is { HasExited:false },id==session && player is { HasExited:false } ? AlphaText.Get("AlphaPreviewActive") : AlphaText.Get("AlphaPreviewClosed"));
    private async Task Send(int control,double time,CancellationToken cancellation)
    {
        var payload=new { protocolVersion=1, control, jsonPath, startTime=time, startAt=DateTime.Now.Ticks, audioSpeed=1, noteSpeed=options.NoteSpeed, touchSpeed=options.TouchSpeed, starSpeed=0,
            editorPlayMethod=0, skin="dx",tapSkin="dx",holdSkin="dx",starSkin="dx", smoothSlideAnime=true, showJudgeInfo=false,showComboInfo=true,comboStatusType=display.Combo?1:0,showJudgeLine=true,showJudgeText=true,
            showAllPerfect=false,showSongDetail=false,showGeneratedMark=false, enableVisualChartEditor=false, innerBackgroundCover=display.BackgroundDim,outerBackgroundCover=1, deferPlaybackStart=control==0, language="zh-CN" };
        var payloadText = JsonSerializer.Serialize(payload);
        await File.WriteAllTextAsync(Path.Combine(work!, "request.json"), payloadText, cancellation);
        using var response=await http.PostAsync(endpoint,new StringContent(payloadText,Encoding.UTF8,"application/json"),cancellation);
        var body=await response.Content.ReadAsStringAsync(cancellation);
        if(!response.IsSuccessStatusCode) throw new InvalidOperationException(AlphaText.Get("AlphaPlayerRejected")+body);
        using var parsed=JsonDocument.Parse(body);
        if(!parsed.RootElement.TryGetProperty("ok",out var ok)||!ok.GetBoolean()) throw new InvalidOperationException(AlphaText.Get("AlphaPlayerLoadFailed")+body);
    }
    private static void CopyChartAssets(string from,string to,CancellationToken cancellation)
    {
        var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){".png",".jpg",".jpeg",".svg",".mp4",".wav",".mp3",".ogg",".json"};
        void Copy(string directory)
        {
            foreach(var file in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                cancellation.ThrowIfCancellationRequested();
                if((file.Attributes & FileAttributes.ReparsePoint)!=0) continue;
                if(file is DirectoryInfo) { Copy(file.FullName); continue; }
                if(!allowed.Contains(file.Extension)||file.Name.EndsWith(".alpha-preview.json",StringComparison.OrdinalIgnoreCase))continue;
                var target=Path.Combine(to,Path.GetRelativePath(from,file.FullName));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file.FullName,target,true);
            }
        }
        Copy(from);
    }
    private async Task Stage(int stage,CancellationToken cancellation)
    {
        window?.SetStage(stage);
        // 同目录原子替换，播放器不会读到写入一半的进度文件。
        var target=Path.Combine(work!,"loading-stage.txt");
        await File.WriteAllTextAsync(target+".tmp",stage.ToString(),cancellation);
        File.Move(target+".tmp",target,true);
    }
    private void StopOwned()
    {
        window?.CloseOwned();window=null;
        if(player!=null) { try { if(!player.HasExited) { player.Kill(true); player.WaitForExit(3000); } } finally { player.Dispose(); player=null; } }
        // 本服务仅清理自己创建的 GUID 目录，留下最近一次日志。
        if (work != null && session != null)
        {
            var root=Path.GetFullPath(Path.Combine(StaticSettings.tempPath,"AlphaPreview"));
            var owned=Path.GetFullPath(work);
            if (Guid.TryParseExact(session,"N",out _) && owned.Equals(Path.Combine(root,session),StringComparison.OrdinalIgnoreCase) && Directory.Exists(owned))
            {
                try
                {
                    var log=Path.Combine(owned,"player.log");
                    if(File.Exists(log)) File.Copy(log,Path.Combine(root,"last-player.log"),true);
                    Directory.Delete(owned,true);
                }
                catch(IOException) { }
                catch(UnauthorizedAccessException) { }
            }
        }
        session=null; endpoint=null; jsonPath=null; work=null;
    }
    public void Dispose() { StopOwned(); http.Dispose(); gate.Dispose(); }
}
