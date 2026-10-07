using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SinmaiAlpha.Preview;

namespace MaiChartManager.Services;

// Own UI thread: decoding WAV / building Unity objects cannot freeze the spinner.
internal sealed class AlphaPreviewLoadingPanel : Control
{
    private readonly System.Windows.Forms.Timer animation=new() { Interval=16 };
    private readonly Stopwatch elapsed=Stopwatch.StartNew();
    private readonly Image arrow;
    private readonly ImageAttributes tint=new();
    private int stage;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Stage {get=>stage;set {stage=value;Invalidate();}}
    internal float Angle=>PreviewLoading.SpinnerAngle(elapsed.Elapsed.TotalSeconds);
    public AlphaPreviewLoadingPanel(string directory)
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        BackColor=Color.Black;
        // Exactly the refresh sprite and tint used by the ordinary chart browser.
        using var original=Image.FromFile(Path.Combine(directory,"76aae32de90228d48aa3588e7ca0b1cc.png"));
        arrow=new Bitmap(original);
        tint.SetColorMatrix(new ColorMatrix {Matrix00=.9724358f,Matrix11=.7216981f,Matrix22=1,Matrix33=1,Matrix44=1});
        animation.Tick+=(_,_)=>Invalidate();animation.Start();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        var x=Width/2f;var y=Height/2f;var size=Math.Clamp(Height*.09f,36,72);
        using var center=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        using var white=new SolidBrush(Color.White);
        g.DrawString(PreviewLoading.Counter(stage),Font,white,new PointF(x,y-size),center);
        var state=g.Save();g.TranslateTransform(x,y);g.RotateTransform(Angle);
        g.DrawImage(arrow,new Rectangle(-(int)size/2,-(int)size/2,(int)size,(int)size),0,0,arrow.Width,arrow.Height,GraphicsUnit.Pixel,tint);g.Restore(state);
        using var detailFont=new Font(Font.FontFamily,16,FontStyle.Regular,GraphicsUnit.Pixel);
        g.DrawString(PreviewLoading.Label(stage),detailFont,white,new PointF(x,y+size),center);
    }
    protected override void OnVisibleChanged(EventArgs e) {base.OnVisibleChanged(e);if(Visible)elapsed.Restart();animation.Enabled=Visible;}
    protected override void Dispose(bool disposing) {if(disposing){animation.Dispose();arrow.Dispose();tint.Dispose();}base.Dispose(disposing);}
}
