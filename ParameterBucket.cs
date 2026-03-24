
using System.ComponentModel;
using System.Numerics;
using System.Xml;

namespace OpenKNXproducer
{
    class ParameterBucket
    {
        BigInteger mBitField;
        List<XmlNode> mParameters = [];

        public ParameterBucket(XmlNode iParameter, CheckHelper iCheck)
        {
            Valid = int.TryParse(iParameter.NodeAttr("Offset"), out int lParamOffset);
            if (!Valid)
                iCheck.WriteFail("Found Union Parameter '{0}' with invalid Offset value '{1}'", iParameter.NodeAttr("Name"), iParameter.NodeAttr("Offset"));
            Valid = int.TryParse(iParameter.NodeAttr("BitOffset"), out int lParamBitOffset) && Valid;
            Valid = Valid && lParamBitOffset < 8;
            if (!Valid)
                iCheck.WriteFail("Found Union Parameter '{0}' with invalid BitOffset value '{1}'", iParameter.NodeAttr("Name"), iParameter.NodeAttr("BitOffset"));
            int lParamSize = ProcessInclude.CalcParamSizeInBit(iParameter, true);

            mBitField = new(1);
            mBitField <<= lParamSize;
            mBitField -= 1;
            mBitField <<= lParamOffset * 8 + lParamBitOffset;
            mParameters.Add(iParameter);
        }

        public BigInteger BitField { get { return mBitField; } }

        public bool Valid { get; private set; }

        public bool Merge(ParameterBucket iBucket)
        {
            if ((iBucket.BitField & mBitField)>0)
            {
                mBitField |= iBucket.BitField;
                mParameters.AddRange(iBucket.mParameters);
                return true;
            } 
            else
                return false;
        }

        public bool Merge(List<ParameterBucket> cBuckets)
        {
            bool lAnyRemoved = false;
            bool lRemoved = true;
            while (lRemoved)
            {
                lRemoved = false;    
                var lEnumerator = cBuckets.GetEnumerator();
                while (lEnumerator.MoveNext())
                {
                    if (Merge(lEnumerator.Current)) 
                    {
                        cBuckets.Remove(lEnumerator.Current);
                        lEnumerator = cBuckets.GetEnumerator();
                        lEnumerator.MoveNext();
                        lRemoved = true;
                        lAnyRemoved = true;
                    }
                    
                }            
            }
            return lAnyRemoved;
        }

        public static bool Merge(List<ParameterBucket> iBuckets, out List<ParameterBucket>eBuckets)
        {
            bool lMerged = false;
            eBuckets = [];
            while (iBuckets.Count > 0) 
            {
                ParameterBucket lBucket = iBuckets[0];
                iBuckets.RemoveAt(0);
                lMerged = lBucket.Merge(iBuckets) || lMerged;
                eBuckets.Add(lBucket);
            }
            return lMerged;
        }

        public void WriteCheck(CheckHelper iCheck, int iOffset, int iSizeInBit, int iCount, int iUnionCount)
        {
            // we write just parameters without nowarn
            List<string> lMessages = [];
            foreach (XmlNode lParameter in mParameters)
            {   
                string lMessage = string.Format("    Parameter {0}, Offset=\"{1}\", BitOffset=\"{2}\"", lParameter.NodeAttr("Name"), lParameter.NodeAttr("Offset"), lParameter.NodeAttr("BitOffset"));
                if (!Program.GetNoWarnAttribute(lParameter) && !CheckHelper.CheckWarnSuppress(10, lMessage))
                    lMessages.Add(lMessage);
            }
            if (lMessages.Count > 0)
            {                
                iCheck.WriteWarn(10, "In Union {3} (SizeInBit=\"{0}\" / Offset=\"{1}\") found overlapping parameter bucket {2}:", iSizeInBit, iOffset, iCount, iUnionCount);
                foreach (string lMessage in lMessages)
                {   
                    iCheck.WriteWarn(10, lMessage);
                }
            }
        }
    }
}