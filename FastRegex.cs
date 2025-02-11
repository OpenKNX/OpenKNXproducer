using System.Text.RegularExpressions;

namespace OpenKNXproducer
{
    public static partial class FastRegex
    {
    [GeneratedRegex(@"Id=""M-00FA_A-[0-9A-F]{4}-([0-9A-F]{2})-")]
    public static partial Regex ApplicationId();

    [GeneratedRegex("xmlns=\"(http:\\/\\/knx\\.org\\/xml\\/project\\/[0-9]{1,2})\"")]
    public static partial Regex EtsProject();

    [GeneratedRegex("(_O-|_UP-|_P-|_R-)")]
    public static partial Regex ParamAndComobjectRef();

    [GeneratedRegex("(_UP-|_P-)")]
    public static partial Regex ParamAndComobjectRefRef();

    [GeneratedRegex("-[0-9A-F]{4}-[0-9A-F]{2}-[0-9A-F]{4}")]
    public static partial Regex IdNamespace();

    [GeneratedRegex(@"%[A-Za-z0-9\-_]*%")]
    public static partial Regex ConfigName();

    [GeneratedRegex(@"(//[ \t]*)?function[ \t\t]*([A-Za-z0-9_]*)\s*\(")]
    public static partial Regex ScriptMethodName();

    [GeneratedRegex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}")]
    public static partial Regex IpAddress();

    [GeneratedRegex("<\\?xml-model.* href=\"(.*.xsd)\" ")]
    public static partial Regex XmlSchema();

    [GeneratedRegex(@"%(C{1,4})(\*\d{1,3})?([\+\-]\d{1,4})?%")]
    public static partial Regex ChannelNumberPattern();

    [GeneratedRegex(@"%(Z{1,4})%")]
    public static partial Regex ChannelLetterPattern();

    [GeneratedRegex(@"%!K(\d{1,3})!C(\d{1,4})!(\w*)!%")]
    public static partial Regex KoTemplateFinal();

    [GeneratedRegex(@"%K(\d{1,3})%")]
    public static partial Regex KoTemplate();

    [GeneratedRegex(@"!?\[.*\]\(.*\)")]
    public static partial Regex LinkPattern();

    [GeneratedRegex("[^a-zA-Z0-9-_ ÄäÖöÜüß\\n]")]
    public static partial Regex DocChapterId();

    [GeneratedRegex("--*")]
    public static partial Regex DocChapterWhitespaces();

    [GeneratedRegex("[#*]")]
    public static partial Regex DocChapterName();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    public static partial Regex DocLink();

    [GeneratedRegex("<!--\\s*DOC\\s*(HelpContext=\"(.*)\")?\\s*-->")]
    public static partial Regex DocStart();

    [GeneratedRegex("<!--\\s*DOCEND\\s*-->")]
    public static partial Regex DocEnd();

    [GeneratedRegex("<!--\\s*DOC\\s*(Skip=\"(.*)\")\\s*-->")]
    public static partial Regex DocSkip();

    [GeneratedRegex("<!--\\s*DOCCONTENT")]
    public static partial Regex DocContentStart();

    [GeneratedRegex("\\s*DOCCONTENT\\s*-->")]
    public static partial Regex DocContentEnd();

    [GeneratedRegex("^(?=\\s*<!--\\s)(?:(?!DOC).)*$")]
    public static partial Regex DocCommentStart();

    [GeneratedRegex(".*-->\\s*")]
    public static partial Regex DocCommentEnd();

    [GeneratedRegex(@"##(#?#?#?)\s*\*\*(.*)\*\*")]
    public static partial Regex DocCleanupTitle();
    }
}
// // No longer partial!
// public class FastRegex
// {
//   public bool DoesItMatch(string input) => MyRegex.Foo().IsMatch(input);
// }