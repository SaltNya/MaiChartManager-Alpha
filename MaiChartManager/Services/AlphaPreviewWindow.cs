using System.Diagnostics;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MaiChartManager.Services;

// 先显示轻量窗口，再接入本次 Unity 子窗口；不预先常驻播放器。
internal sealed class AlphaPreviewWindow : Form
{
    private readonly AlphaPreviewLoadingPanel loading;
    private readonly PrivateFontCollection fonts = new();
    private IntPtr child;
    private Rectangle windowBounds;
    private bool fullscreen;
    private readonly System.Windows.Forms.Timer timer = new() { Interval=250 };
    private Process? player;
    public readonly CancellationTokenSource ClosedToken = new();
    public IntPtr HostHandle { get; private set; }

    private AlphaPreviewWindow(string uiDirectory)
    {
        loading=new AlphaPreviewLoadingPanel(uiDirectory) { Dock=DockStyle.Fill };
        Text=AlphaText.Get("AlphaPreviewTitle");BackColor=Color.Black;
        Width=960;Height=640;MinimumSize=new Size(480,360);
        StartPosition=FormStartPosition.Manual;Location=WebViewHelper.CalculatePosition(Width,Height);
        try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        var fontFile=Path.Combine(uiDirectory,"Minimoon.ttf");
        if(File.Exists(fontFile)) { fonts.AddFontFile(fontFile);loading.Font=new Font(fonts.Families[0],24,FontStyle.Regular,GraphicsUnit.Pixel); }
        else loading.Font=new Font(FontFamily.GenericSansSerif,24,FontStyle.Regular,GraphicsUnit.Pixel);
        Controls.Add(loading);
        timer.Tick+=(_,_)=> { try { if(player is { HasExited:true }) Close(); } catch(InvalidOperationException) {Close();} };
        ClientSizeChanged+=(_,_)=>ResizePlayer();
    }
    public static Task<AlphaPreviewWindow> Open(string uiDirectory)
    {
        var ready=new TaskCompletionSource<AlphaPreviewWindow>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=> {
            try {
                using var window=new AlphaPreviewWindow(uiDirectory);
                window.Shown+=(_,_)=> { window.HostHandle=window.Handle;window.Refresh();ready.TrySetResult(window); };
                Application.Run(window);
            } catch(Exception e) { ready.TrySetException(e); }
        }) { IsBackground=true,Name="Alpha preview window" };
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return ready.Task;
    }
    private void Post(Action action) { if(!IsDisposed&&IsHandleCreated)try { BeginInvoke(action); } catch(InvalidOperationException) { } }
    public void SetStage(int stage)=>Post(()=>loading.Stage=stage);
    public void RevealPlayer()=>Post(()=>loading.Hide());
    public void CloseOwned()=>Post(Close);
    public void Watch(Process process)=>Post(()=> {player=process;timer.Start();});
    public Task Attach(Process process)
    {
        var result=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(()=> {
            try {
                IntPtr found=IntPtr.Zero;
                EnumWindows((handle,_)=> { GetWindowThreadProcessId(handle,out var id);if(id==(uint)process.Id) {var name=new System.Text.StringBuilder(128);GetClassName(handle,name,name.Capacity);if(name.ToString()=="UnityWndClass")found=handle;}return true; },IntPtr.Zero);
                if(found==IntPtr.Zero)throw new IOException(AlphaText.Get("AlphaPlayerWindowMissing"));
                child=found;
                var style=GetWindowLongPtr(child,-16).ToInt64();
                SetWindowLongPtr(child,-16,new IntPtr((style&~0x80CF0000L)|0x40000000L));
                SetParent(child,Handle);ResizePlayer();ShowWindow(child,5);
                // Keep the independent animated overlay until Unity has rendered the chart.
                loading.BringToFront();SetWindowPos(loading.Handle,IntPtr.Zero,0,0,0,0,0x0043);result.SetResult();
            } catch(Exception e) { result.SetException(e); }
        });
        return result.Task.WaitAsync(ClosedToken.Token);
    }
    private void ResizePlayer() { if(child!=IntPtr.Zero)SetWindowPos(child,IntPtr.Zero,0,0,ClientSize.Width,ClientSize.Height,0x0034); }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x8001) {
            if(!fullscreen) {windowBounds=Bounds;FormBorderStyle=FormBorderStyle.None;Bounds=Screen.FromHandle(Handle).Bounds;}
            else {FormBorderStyle=FormBorderStyle.Sizable;Bounds=windowBounds;}
            fullscreen=!fullscreen;ResizePlayer();return;
        }
        base.WndProc(ref m);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) {timer.Stop();ClosedToken.Cancel();base.OnFormClosed(e);}
    protected override void Dispose(bool disposing) {if(disposing){timer.Dispose();loading.Font.Dispose();fonts.Dispose();}base.Dispose(disposing);}
    private delegate bool EnumWindow(IntPtr handle,IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback,IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle,out uint id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr handle,System.Text.StringBuilder name,int capacity);
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr handle,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr handle,int index,IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle,int command);
}
