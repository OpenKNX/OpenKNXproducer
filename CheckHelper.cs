using System.Text.RegularExpressions;
using System.Xml;

namespace OpenKNXproducer
{
    
    class CheckHelper
    {
        public static readonly Dictionary<uint, HashSet<string>> NoWarn = new();

        public static readonly HashSet<string> sDuplicateMessages = new();

        private bool mFail;
        private bool mWarn;

        private bool mFailAny;

        public bool IsFail
        {
            get { return mFailAny; }
        }

        private Version mMinProducerVersion = null;

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
            string lMessage = string.Format(iFormat, iParams);
            if (!sDuplicateMessages.Contains(lMessage))
            {
                if (!mFail && !mWarn) Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  --> " + lMessage);
                Console.ResetColor();
                sDuplicateMessages.Add(lMessage);
                mFail = true;
                mFailAny = true;
            }
        }

        public void WriteWarn(uint iWarnId, string iFormat, params object[] iParams)
        {
            bool lSuppress = false;
            string lMessage = String.Format($"  --> WARN {iWarnId:d03}: " + iFormat, iParams);
            if (sDuplicateMessages.Contains(lMessage))
                lSuppress = true;
            else lSuppress = CheckWarnSuppress(iWarnId, lMessage);
            if (!lSuppress)
            {
                if (!mFail && !mWarn) Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(lMessage);
                Console.ResetColor();
                sDuplicateMessages.Add(lMessage);
                mWarn = true;
            }
        }

        public static bool CheckWarnSuppress(uint iWarnId, string lMessage)
        {
            bool lSuppress = false;

            if (NoWarn.TryGetValue(iWarnId, out HashSet<string> lPatterns) && iWarnId != 9) // && iWarnId != 10 ) // Warn 9 (Union size) and 10 (Parameter overlap) should not be suppressed by default, as it is often an indication of a real problem in the document
            {
                foreach (var lPattern in lPatterns)
                {
                    Regex lRegex = new(lPattern);
                    Match lMatch = lRegex.Match(lMessage);
                    lSuppress = lMatch.Success;
                    if (lSuppress) break;
                }
            }

            return lSuppress;
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

        public bool CheckVersion(string iVersionString)
        {
            return CheckVersion(new Version(iVersionString));
        }

        public bool CheckVersion(Version iVersion)
        {
            return mMinProducerVersion >= iVersion;
        }

        public Version MinProducerVersion { 
            get
            {
                if (mMinProducerVersion == null)
                {
                    mMinProducerVersion = new(0, 0, 0);
                    foreach (XmlNode lNode in ProcessInclude.MinVersionNodes)
                    {
                        if (lNode.LocalName == "minOpenKNXproducerVersion")
                        {
                            Version lVersion = new(lNode.NodeAttr("value", "0.0.0") + ".0");
                            if (lVersion > mMinProducerVersion)
                                mMinProducerVersion = lVersion;
                        }
                    }

                }
                return mMinProducerVersion;
            } 
        }
    }
}