using System.Text.RegularExpressions;

class CheckHelper
{
    public static readonly Dictionary<uint, HashSet<string>> NoWarn = new();

    private bool mFail;
    private bool mWarn;

    private bool mFailAny;

    public bool IsFail
    {
        get { return mFailAny; }
    }

    public void Reset()
    {
        mFail = false;
        mWarn = false;
    }

    public static bool AddNoWarn(uint iId, string iRegex)
    {
        bool lResult = false;
        if (NoWarn.ContainsKey(iId) && !NoWarn[iId].Contains(iRegex))
        {
            NoWarn[iId].Add(iRegex);
            lResult = true;
        }
        else
        {
            NoWarn[iId] = new() { iRegex };
            lResult = true;
        }
        return lResult;
    }


    public void WriteFail(string iFormat, params object[] iParams)
    {
        if (!mFail && !mWarn) Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  --> " + iFormat, iParams);
        Console.ResetColor();
        mFail = true;
        mFailAny = true;
    }

    public void WriteWarn(uint iWarnId, string iFormat, params object[] iParams)
    {
        bool lSuppress = false;
        string lMessage = String.Format($"  --> WARN {iWarnId:d03}: " + iFormat, iParams);
        if (NoWarn.ContainsKey(iWarnId))
        {
            foreach (var lPattern in NoWarn[iWarnId])
            {
                Regex lRegex = new(lPattern);
                Match lMatch = lRegex.Match(lMessage);
                lSuppress = lMatch.Success;
                if (lSuppress) break;
            }
        }
        if (!lSuppress)
        {
            if (!mFail && !mWarn) Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(lMessage);
            Console.ResetColor();
            mWarn = true;
        }
    }

    public void Start(string iTitle)
    {
        Reset();
        Console.Write(iTitle);
    }

    public void Finish(string iOk = "OK")
    {

        if (!mFail && !mWarn)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(" " + iOk);
            Console.ResetColor();
        }
        Reset();
    }



}