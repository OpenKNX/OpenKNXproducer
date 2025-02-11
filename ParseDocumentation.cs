using System.Text;
using System.Text.RegularExpressions;

namespace OpenKNXproducer
{
    public class ParseDocumentation
    {

        static readonly Dictionary<string, string> sCharReplace = new() { { " ", "-" }, { "\n", "-" }, { "Ä", "Ae" }, { "ä", "ae" }, { "Ö", "Oe" }, { "ö", "oe" }, { "Ü", "Ue" }, { "ü", "ue" }, { "ß", "ss" } };
        public static string GetChapterId(string iLine, string iPraefix)
        {
            // get rid of forbidden characters
            string lResult = FastRegex.DocChapterId().Replace(iLine, "");
            // get rid of whitespaces at start and end
            lResult = lResult.Trim();
            foreach (var lEntry in sCharReplace)
                lResult = lResult.Replace(lEntry.Key, lEntry.Value);
            if (iPraefix != "") lResult = iPraefix + "-" + lResult;
            lResult = FastRegex.DocChapterWhitespaces().Replace(lResult, "-");
            return lResult;
        }

        private static string GetChapterName(string iLine)
        {
            string lResult = FastRegex.DocChapterName().Replace(iLine, "").Trim();
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
            string lBaggage = iBaggage.ToString();
            lBaggage = FastRegex.DocLink().Replace(lBaggage, "$1");
            iFileName = Path.ChangeExtension(iFileName, "md");
            File.WriteAllText(Path.Combine(iPath, iFileName), lBaggage, Encoding.UTF8);
        }

        public static int ExportBaggages(string iWorkingDir, string iBaggageDir, string iDocFileName, string iPraefix)
        {
            // we read all lines of doc file
            string lBaggageFileName = "";
            string lChapterName = "";
            Match lMatch;
            StringBuilder lBaggage = new();

            using var lFile = File.OpenText(iDocFileName);
            int lActiveDoc = 0;
            int lActiveSkip = 0;
            bool lActiveContent = false;
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
                    lMatch = FastRegex.DocSkip().Match(lLine);
                    if (lMatch.Success)
                    {
                        lActiveSkip = int.Parse(lMatch.Groups[2].Value);
                        continue;
                    }
                }
                // skip commented lines
                if (!lActiveComment)
                {
                    lMatch = FastRegex.DocCommentStart().Match(lLine);
                    lActiveComment = lMatch.Success;
                }
                if (lActiveComment)
                {
                    if (!lActiveContent)
                    {
                        lMatch = FastRegex.DocContentStart().Match(lLine);
                        if (lMatch.Success)
                        {
                            lActiveContent = true;
                            continue;
                        }
                    }
                    if (lActiveContent)
                    {
                        lMatch = FastRegex.DocContentEnd().Match(lLine);
                        if (lMatch.Success)
                        {
                            lActiveContent = false;
                            continue;
                        }
                    }
                    lMatch = FastRegex.DocCommentEnd().Match(lLine);
                    if (lMatch.Success)
                    {
                        lActiveComment = false;
                        lActiveContent = false;
                    }
                    if (!lActiveContent)
                        continue;
                }
                // process document lines
                lMatch = FastRegex.DocStart().Match(lLine);
                if (lMatch.Success)
                {
                    if (lActiveDoc > 0)
                    {
                        // we have a new chapter, but the old is still active, we save the old one
                        lActiveDoc = 0;
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine(lBaggageFileName);
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
                    Console.WriteLine("--> {0}.md", lBaggageFileName);
                    Console.WriteLine();
                    string lChapterNameFormatted = $"### {lChapterName}";
                    lBaggage.AppendLine(lChapterNameFormatted);
                    lBaggage.AppendLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(lChapterNameFormatted);
                    Console.WriteLine();
                    Console.ResetColor();
                }
                else if (lActiveDoc > 0)
                {
                    lMatch = FastRegex.DocEnd().Match(lLine);
                    if (lFile.EndOfStream || lMatch.Success || (lLine.StartsWith("#") && lActiveDoc >= CountCharAtStart(lLine, '#')))
                    {
                        // chapter is ended
                        lActiveDoc = 0;
                        Console.WriteLine("");
                        Console.WriteLine("");
                        Console.WriteLine("");
                        WriteBaggage(iBaggageDir, lBaggageFileName, lBaggage);
                        lBaggage.Clear();
                        continue;
                    }
                    if (!lActiveContent)
                    {
                        lMatch = FastRegex.DocContentStart().Match(lLine);
                        if (lMatch.Success)
                        {
                            lActiveContent = true;
                            continue;
                        }
                    }
                    if (lActiveContent)
                    {
                        lMatch = FastRegex.DocContentEnd().Match(lLine);
                        if (lMatch.Success)
                        {
                            lActiveContent = false;
                            continue;
                        }
                    }
                }

                if (lActiveDoc > 0 && lActiveSkip == 0)
                {
                    lMatch = FastRegex.DocCleanupTitle().Match(lLine);
                    if (lMatch.Success)
                        lLine = lLine.Replace("**", "");
                    lBaggage.AppendLine(lLine);
                    Console.WriteLine(lLine);
                }
            }
            return 0;
        }
    }
}