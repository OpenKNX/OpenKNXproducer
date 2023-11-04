class CheckHelper
{
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

    public void WriteFail(string iFormat, params object[] iParams)
    {
        if (!mFail && !mWarn) Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  --> " + iFormat, iParams);
        Console.ResetColor();
        mFail = true;
        mFailAny = true;
    }

    public void WriteWarn(string iFormat, params object[] iParams)
    {
        if (!mFail && !mWarn) Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  --> WARN: " + iFormat, iParams);
        Console.ResetColor();
        mWarn = true;
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