using System.Text;
using System.Text.RegularExpressions;

namespace OpenKNXproducer
{
    public class ParseDocumentation
    {

        static readonly Regex sRegexChapterId = new("[^a-zA-Z0-9-_ ÄäÖöÜüß]");
        static readonly Regex sRegexChapterName = new("[#*]");
        static readonly Dictionary<string, string> sCharReplace = new() { { " ", "-" }, { "Ä", "Ae" }, { "ä", "ae" }, { "Ö", "Oe" }, { "ö", "oe" }, { "Ü", "Ue" }, { "ü", "ue" }, { "ß", "ss" } };
        public static string GetChapterId(string iLine, string iPraefix)
        {
            // get rid of forbidden characters
            string lResult = sRegexChapterId.Replace(iLine, "");
            // get rid of whitespaces at start and end
            lResult = lResult.Trim();
            foreach (var lEntry in sCharReplace)
                lResult = lResult.Replace(lEntry.Key, lEntry.Value);
            if (iPraefix != "") lResult = iPraefix + "-" + lResult;
            return lResult;
        }

        private static string GetChapterName(string iLine)
        {
            string lResult = sRegexChapterName.Replace(iLine, "").Trim();
            return lResult;
        }

        private static int CountCharAtStart(string iLine, char iChar)
        {
            int lResult = 0;
            foreach (var lChar in iLine.ToCharArray())
            {
                if (lChar == iChar)
                    lResult++;
                else
                    break;
            }
            if (lResult == 0) lResult = 10;
            return lResult;
        }

        private static void WriteBaggage(string iPath, string iFileName, StringBuilder iBaggage)
        {
            // ensure right extension
            iFileName = Path.ChangeExtension(iFileName, "md");
            File.WriteAllText(Path.Combine(iPath, iFileName), iBaggage.ToString(), Encoding.UTF8);
        }

        public static int ExportBaggages(string iWorkingDir, string iBaggageDir, string iDocFileName, string iPraefix)
        {
            // we read all lines of doc file
            string lBaggageFileName = "";
            string lChapterName = "";
            Match lMatch;
            StringBuilder lBaggage = new();

            using var lFile = File.OpenText(iDocFileName);
            Regex lRegexDocStart = new("<!--\\s*DOC\\s*(HelpContext=\"(.*)\")?\\s*-->");
            Regex lRegexDocEnd = new("<!--\\s*DOCEND\\s*-->");
            int lActiveDoc = 0;
            Regex lRegexDocSkip = new("<!--\\s*DOC\\s*(Skip=\"(.*)\")\\s*-->");
            int lActiveSkip = 0;
            Regex lRegexDocContentStart = new("<!--\\s*DOCCONTENT");
            Regex lRegexDocContentEnd = new("\\s*DOCCONTENT\\s*-->");
            bool lActiveContent = false;
            Regex lRegexCommentStart = new("^(?=\\s*<!--\\s)(?:(?!DOC).)*$");
            Regex lRegexCommentEnd = new(".*-->\\s*");
            bool lActiveComment = false;

            string lLine = "";
            while (!lFile.EndOfStream)
            {
                if (lActiveSkip < 0)
                    lActiveSkip = 0;
                else
                    lLine = lFile.ReadLine();
                // skip n lines, we use this from external command or internally
                if (lActiveSkip > 0)
                {
                    lActiveSkip--;
                    continue;
                }
                if (lActiveSkip == 0)
                {
                    lMatch = lRegexDocSkip.Match(lLine);
                    if (lMatch.Success)
                    {
                        lActiveSkip = int.Parse(lMatch.Groups[2].Value);
                        continue;
                    }
                }
                // skip commented lines
                if (!lActiveComment)
                {
                    lMatch = lRegexCommentStart.Match(lLine);
                    lActiveComment = lMatch.Success;
                }
                if (lActiveComment)
                {
                    if (!lActiveContent)
                    {
                        lMatch = lRegexDocContentStart.Match(lLine);
                        if (lMatch.Success)
                        {
                            lActiveContent = true;
                            continue;
                        }
                    }
                    if (lActiveContent)
                    {
                        lMatch = lRegexDocContentEnd.Match(lLine);
                        if (lMatch.Success)
                        {
                            lActiveContent = false;
                            continue;
                        }
                    }
                    lMatch = lRegexCommentEnd.Match(lLine);
                    if (lMatch.Success)
                    {
                        lActiveComment = false;
                        lActiveContent = false;
                    }
                    if (!lActiveContent)
                        continue;
                }
                // process document lines
                lMatch = lRegexDocStart.Match(lLine);
                if (lMatch.Success)
                {
                    if (lActiveDoc > 0)
                    {
                        // we have a new chapter, but the old is still active, we save the old one
                        lActiveDoc = 0;
                        Console.WriteLine("\n\n", lBaggageFileName);
                        WriteBaggage(iBaggageDir, lBaggageFileName, lBaggage);
                        lBaggage.Clear();
                    }
                    // we expect a chapter header line starting with a number of '#'
                    lLine = lFile.ReadLine();
                    lActiveDoc = CountCharAtStart(lLine, '#');

                    // calculate baggages filename
                    if (lMatch.Groups.Count == 3 && lMatch.Groups[2].Value != "")
                    {
                        lBaggageFileName = GetChapterId(lMatch.Groups[2].Value, iPraefix);
                        // Add chapter start for non chapter blocks
                        if (lLine.StartsWith("#"))
                        {
                            lChapterName = GetChapterName(lLine);
                            lActiveSkip = 1;
                        }
                        else
                        {
                            lChapterName = GetChapterName(lMatch.Groups[2].Value);
                            lActiveSkip = -1;
                        }
                    }
                    else if (lLine.StartsWith("#"))
                    {
                        // name is extracted from title
                        lBaggageFileName = GetChapterId(lLine, iPraefix);
                        lChapterName = GetChapterName(lLine);
                        lActiveSkip = 1;
                    }
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("--> {0}.md\n", lBaggageFileName);
                    string lChapterNameFormatted = string.Format("### **{0}**\n", lChapterName);
                    lBaggage.AppendLine(lChapterNameFormatted);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(lChapterNameFormatted);
                    Console.ResetColor();
                }
                else if (lActiveDoc > 0)
                {
                    lMatch = lRegexDocEnd.Match(lLine);
                    if (lFile.EndOfStream || lMatch.Success || (lLine.StartsWith("#") && lActiveDoc >= CountCharAtStart(lLine, '#')))
                    {
                        // chapter is ended
                        lActiveDoc = 0;
                        Console.WriteLine("\n\n");
                        WriteBaggage(iBaggageDir, lBaggageFileName, lBaggage);
                        lBaggage.Clear();
                        continue;
                    }
                    if (!lActiveContent)
                    {
                        lMatch = lRegexDocContentStart.Match(lLine);
                        if (lMatch.Success)
                        {
                            lActiveContent = true;
                            continue;
                        }
                    }
                    if (lActiveContent)
                    {
                        lMatch = lRegexDocContentEnd.Match(lLine);
                        if (lMatch.Success)
                        {
                            lActiveContent = false;
                            continue;
                        }
                    }
                }

                if (lActiveDoc > 0 && lActiveSkip == 0)
                {
                    lBaggage.AppendLine(lLine);
                    Console.WriteLine(lLine);
                }
            }
            return 0;
        }
    }
}