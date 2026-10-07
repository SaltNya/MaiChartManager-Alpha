extern alias Alpha;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using P=Alpha::MuConvert.Antlr.SimaiParser;
using L=Alpha::MuConvert.Antlr.SimaiLexer;

namespace MaiChartManager.Controllers.Charts.Services;

public static class AlphaContentDetector
{
    // 仅查看 inote 的语法，不执行 Alpha 转谱。标题、作者、注释不参与判断。
    public static bool ContainsAlpha(string inote)
    {
        var text=StripComments(inote);
        var lexer=new L(new AntlrInputStream(text));lexer.RemoveErrorListeners();
        var tokens=new CommonTokenStream(lexer);tokens.Fill();
        foreach(var t in tokens.GetTokens()) {
            if(t.Type is L.COMMAND or L.OVERLAY_STREAM or L.WAVE_TIME_SIG or L.NOISE_ZONE or L.D_ZONE or L.SLIDE_CODE or L.NOTE_SKIN or L.TOUCH_RADIUS or L.BORROW_PATH)return true;
            if(t.Type==L.MODIFIER && t.Text is "m" or "c")return true;
            if(t.Type==L.SLIDE_TYPE && (t.Text.StartsWith('P')||t.Text.StartsWith('Q')||t.Text is "rp" or "rq"||t.Text.EndsWith('d')||t.Text.Length>1&&(t.Text[0] is '<' or '>')))return true;
        }
        var parser=new P(tokens);parser.RemoveErrorListeners();
        var pending=new Stack<IParseTree>();pending.Push(parser.chart());
        while(pending.TryPop(out var node)) {
            if(node is P.HoldSlideHeadContext or P.TouchHoldSlideHeadContext or P.TouchHeadContext or P.TouchStarContext or P.TapHoldContext)return true;
            if(node is P.SlideBodyContext body && body.children?.OfType<ITerminalNode>().Any(t=>t.Symbol.Type==L.TOUCH_AREA)==true)return true;
            if(node is P.SharedHeadSlideContext shared && shared.KEY()!=null)return true;
            if(node is P.TapContext or P.HoldContext) {
                if(node.GetText().Contains('f'))return true;
            }
            if(node is P.TouchContext or P.TouchHoldContext) {
                if(node.GetText().IndexOfAny(['b','x','$','@'])>=0)return true;
            }
            for(var i=node.ChildCount-1;i>=0;i--)pending.Push(node.GetChild(i));
        }
        // 原版直线不包含同键和相邻键；检查链段时忽略时值方括号。
        var compact=Regex.Replace(Regex.Replace(text,@"\[[^\]]*\]",""),@"\s+","");
        foreach(Match match in Regex.Matches(compact,@"(?=([1-8])[bxf$@?!]*-([1-8]))")) {
            var distance=Math.Abs(match.Groups[1].Value[0]-match.Groups[2].Value[0]);
            if(distance is 0 or 1 or 7)return true;
        }
        return false;
    }
    private static string StripComments(string text)
    {
        var chars=text.ToCharArray();var square=0;var brace=0;
        for(var i=0;i<text.Length;i++) {
            // 命令中的 # 颜色和文字不是注释。
            if(text[i]=='<' && Regex.IsMatch(text[i..],@"^<[A-Za-z]+\*")) {
                var end=text.IndexOf('>',i);if(end>=0){i=end;continue;}
            }
            if(text[i]=='[')square++;else if(text[i]==']')square=Math.Max(0,square-1);
            if(text[i]=='{')brace++;else if(text[i]=='}')brace=Math.Max(0,brace-1);
            if(i+1<text.Length && text[i]=='|' && text[i+1]=='*') {
                var end=text.IndexOf("*|",i+2,StringComparison.Ordinal);end=end<0?text.Length:end+2;
                for(;i<end;i++)if(chars[i]!='\r'&&chars[i]!='\n')chars[i]=' ';i--;continue;
            }
            if(i+1<text.Length && text[i]=='|'&&text[i+1]=='|' || text[i]=='#'&&square==0&&brace==0) {
                for(;i<text.Length&&text[i]!='\r'&&text[i]!='\n';i++)chars[i]=' ';i--;
            }
        }
        return new string(chars);
    }
}
