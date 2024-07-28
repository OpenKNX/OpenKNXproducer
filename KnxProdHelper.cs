using OpenKNXproducer.Signing;
using System.Xml.Linq;
using System.Xml;
using System.Text.RegularExpressions;
using System.Xml.Schema;

namespace OpenKNXproducer
{
    internal static class KnxProdHelper
    {
        struct EtsVersion
        {
            public EtsVersion(string iSubdir, string iEts)
            {
                Subdir = iSubdir;
                ETS = iEts;
            }

            public string Subdir { get; private set; }
            public string ETS { get; private set; }
        }

        private static Dictionary<string, EtsVersion> EtsVersions = new Dictionary<string, EtsVersion>() {
            {"http://knx.org/xml/project/11", new EtsVersion("4.0.1997.50261", "ETS 4")},
            {"http://knx.org/xml/project/12", new EtsVersion("5.0.204.12971", "ETS 5")},
            {"http://knx.org/xml/project/13", new EtsVersion("5.1.84.17602", "ETS 5.5")},
            {"http://knx.org/xml/project/14", new EtsVersion("5.6.241.33672", "ETS 5.6")},
            {"http://knx.org/xml/project/20", new EtsVersion("5.7", "ETS 5.7")},
            {"http://knx.org/xml/project/21", new EtsVersion("6.0", "ETS 6.0")},
            {"http://knx.org/xml/project/22", new EtsVersion("6.1", "ETS 6.1")},
            {"http://knx.org/xml/project/23", new EtsVersion("6.2", "ETS 6.2")}
        };

        //installation path of a valid ETS instance (only ETS5 or ETS6 supported)
        private static List<string> gPathETS = new List<string> {
            @"C:\Program Files (x86)\ETS6",
            @"C:\Program Files (x86)\ETS5",
            @"C:\Program Files\ETS6",
            @"C:\Program Files\ETS5",
            AppDomain.CurrentDomain.BaseDirectory
        };

        public static string GetAbsWorkingDir(string iFilename)
        {
            string lResult = Path.GetFullPath(iFilename);
            lResult = Path.GetDirectoryName(lResult);
            return lResult;
        }

        public static string FindEtsPath(string lXmlns)
        {
            string lResult = "";

            int lProjectVersion = int.Parse(lXmlns.Substring(27));

            if (EtsVersions.ContainsKey(lXmlns))
            {
                string lEts = "";
                string lPath = "";

                if (Environment.Is64BitOperatingSystem)
                    lPath = @"C:\Program Files (x86)\ETS6";
                else
                    lPath = @"C:\Program Files\ETS6";
                //if we found an ets6, we can generate all versions with it
                if (Directory.Exists(lPath))
                {
                    lResult = lPath;
                    lEts = "ETS 6.x";
                }

                //if we found ets6 dlls, we can generate all versions with it
                if (Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CV", "6.0")))
                {
                    lResult = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CV", "6.0");
                    lEts = "ETS 6 (local)";
                }

                //else search for an older ETS or CV files
                if (string.IsNullOrEmpty(lResult))
                {
                    string lSubdir = EtsVersions[lXmlns].Subdir;
                    lEts = EtsVersions[lXmlns].ETS;

                    foreach (string path in gPathETS)
                    {
                        if (!Directory.Exists(path)) continue;
                        if (Directory.Exists(Path.Combine(path, "CV", lSubdir))) //If subdir exists everything ist fine
                        {
                            lResult = Path.Combine(path, "CV", lSubdir);
                            break;
                        }
                        else
                        { //otherwise it might be the file in the root folder
                            if (!File.Exists(Path.Combine(path, "Knx.Ets.XmlSigning.dll"))) continue;
                            System.Diagnostics.FileVersionInfo versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(path, "Knx.Ets.XmlSigning.dll"));
                            string newVersion = versionInfo.FileVersion;
                            if (lSubdir.Split('.').Length == 2) newVersion = string.Join('.', newVersion.Split('.').Take(2));
                            // if(newVersion.Split('.').Length != 4) newVersion += ".0";

                            if (lSubdir == newVersion)
                            {
                                lResult = path;
                                break;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(lResult))
                    Console.WriteLine("Found namespace {1} in xml, using {0} for conversion... (Path: {2})", lEts, lXmlns, lResult);
            }
            if (string.IsNullOrEmpty(lResult)) Console.WriteLine("No valid conversion engine available for xmlns {0}", lXmlns);

            return lResult;
        }

        public static int ExportKnxprod(string iPathETS, string iWorkingDir, string iKnxprodFileName, string lTempXmlFileName, string iBaggageName, string iXsdFileName, bool iIsDebug, bool iAutoXsd)
        {
            if (iPathETS == "")
            {
                Console.WriteLine("No ETS found, skipped knxprod creation!");
                return 0;
            }
            try
            {
                if (ValidateXsd(iWorkingDir, lTempXmlFileName, lTempXmlFileName, iXsdFileName, iAutoXsd)) return 1;

                Console.WriteLine("Generating knxprod file...");

                XDocument xdoc = null;
                string xmlContent = File.ReadAllText(lTempXmlFileName);
                xdoc = XDocument.Parse(xmlContent, LoadOptions.SetLineInfo);

                XNode lXmlModel = xdoc.FirstNode;
                if (lXmlModel.NodeType == XmlNodeType.ProcessingInstruction)
                    lXmlModel.Remove();

                string ns = xdoc.Root.Name.NamespaceName;
                XElement xmanu = xdoc.Root.Element(XName.Get("ManufacturerData", ns)).Element(XName.Get("Manufacturer", ns));

                string manuId = xmanu.Attribute("RefId").Value;
                string localPath = AppDomain.CurrentDomain.BaseDirectory;
                if (Directory.Exists(Path.Combine(localPath, "Temp")))
                    Directory.Delete(Path.Combine(localPath, "Temp"), true);

                Directory.CreateDirectory(Path.Combine(localPath, "Temp"));
                Directory.CreateDirectory(Path.Combine(localPath, "Temp", manuId)); //Get real Manu


                XElement xcata = xmanu.Element(XName.Get("Catalog", ns));
                XElement xhard = xmanu.Element(XName.Get("Hardware", ns));
                XElement xappl = xmanu.Element(XName.Get("ApplicationPrograms", ns));
                XElement xbagg = xmanu.Element(XName.Get("Baggages", ns));

                List<XElement> xcataL = new List<XElement>();
                List<XElement> xhardL = new List<XElement>();
                List<XElement> xapplL = new List<XElement>();
                List<XElement> xbaggL = new List<XElement>();
                XElement xlangs = xmanu.Element(XName.Get("Languages", ns));

                if (xlangs != null)
                {
                    xlangs.Remove();
                    foreach (XElement xTrans in xlangs.Descendants(XName.Get("TranslationUnit", ns)).ToList())
                    {
                        DocumentCategory lCategory = GetDocumentCategory(xTrans);
                        switch (lCategory)
                        {
                            case DocumentCategory.Catalog:
                                AddTranslationUnit(xTrans, xcataL, ns);
                                break;
                            case DocumentCategory.Hardware:
                                AddTranslationUnit(xTrans, xhardL, ns);
                                break;
                            case DocumentCategory.Application:
                                AddTranslationUnit(xTrans, xapplL, ns);
                                break;
                            default:
                                throw new Exception("Unknown Translation Type: " + lCategory.ToString());
                        }

                    }
                }
                xhard.Remove();
                if (xbagg != null) xbagg.Remove();

                //Save Catalog
                xappl.Remove();
                if (xcataL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach (XElement xlang in xcataL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, "Catalog.xml"));
                if (xcataL.Count > 0) xlangs.Remove();
                xcata.Remove();

                // Save Hardware
                xmanu.Add(xhard);
                if (xhardL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach (XElement xlang in xhardL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, "Hardware.xml"));
                if (xhardL.Count > 0) xlangs.Remove();
                xhard.Remove();

                if (xbagg != null)
                {
                    // Save Baggages
                    xmanu.Add(xbagg);
                    if (xbaggL.Count > 0)
                    {
                        xlangs.Elements().Remove();
                        foreach (XElement xlang in xbaggL)
                            xlangs.Add(xlang);
                        xmanu.Add(xlangs);
                    }
                    xdoc.Save(Path.Combine(localPath, "Temp", manuId, "Baggages.xml"));
                    if (xbaggL.Count > 0) xlangs.Remove();
                    xbagg.Remove();
                }

                xmanu.Add(xappl);
                if (xapplL.Count > 0)
                {
                    xlangs.Elements().Remove();
                    foreach (XElement xlang in xapplL)
                        xlangs.Add(xlang);
                    xmanu.Add(xlangs);
                }
                string appId = xappl.Elements(XName.Get("ApplicationProgram", ns)).First().Attribute("Id").Value;
                xdoc.Save(Path.Combine(localPath, "Temp", manuId, $"{appId}.xml"));
                if (xapplL.Count > 0) xlangs.Remove();

                // Copy baggages to output dir
                string lSourceBaggageName = Path.Combine(iWorkingDir, iBaggageName);
                var lSourceBaggageDir = new DirectoryInfo(lSourceBaggageName);
                if (lSourceBaggageDir.Exists)
                    lSourceBaggageDir.DeepCopy(Path.Combine(localPath, "Temp", manuId, "Baggages"));

                IDictionary<string, string> applProgIdMappings = new Dictionary<string, string>();
                IDictionary<string, string> applProgHashes = new Dictionary<string, string>();
                IDictionary<string, string> mapBaggageIdToFileIntegrity = new Dictionary<string, string>(50);

                FileInfo hwFileInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, "Hardware.xml"));
                FileInfo catalogFileInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, "Catalog.xml"));
                FileInfo appInfo = new FileInfo(Path.Combine(localPath, "Temp", manuId, $"{appId}.xml"));

                int nsVersion = int.Parse(ns.Substring(ns.LastIndexOf('/') + 1));
                ApplicationProgramHasher aph = new ApplicationProgramHasher(appInfo, mapBaggageIdToFileIntegrity, iPathETS, nsVersion, true);
                aph.Hash();

                applProgIdMappings.Add(aph.OldApplProgId, aph.NewApplProgId);
                if (!applProgHashes.ContainsKey(aph.NewApplProgId))
                    applProgHashes.Add(aph.NewApplProgId, aph.GeneratedHashString);

                HardwareSigner hws = new HardwareSigner(hwFileInfo, applProgIdMappings, applProgHashes, iPathETS, nsVersion, true);
                hws.SignFile();
                IDictionary<string, string> hardware2ProgramIdMapping = hws.OldNewIdMappings;

                CatalogIdPatcher cip = new CatalogIdPatcher(catalogFileInfo, hardware2ProgramIdMapping, iPathETS, nsVersion);
                cip.Patch();

                XmlSigning.SignDirectory(Path.Combine(localPath, "Temp", manuId), iPathETS);

                Directory.CreateDirectory(Path.Combine(localPath, "Masters"));
                ns = ns.Substring(ns.LastIndexOf("/") + 1);
                // Console.WriteLine("localPath is {0}", localPath);
                if (!File.Exists(Path.Combine(localPath, "Masters", $"project-{ns}.xml")))
                {
                    // var client = new System.Net.WebClient();
                    // client.DownloadFile($"https://update.knx.org/data/XML/project-{ns}/knx_master.xml", Path.Combine(localPath, "Masters", $"project-{ns}.xml"));
                    HttpClient client = new HttpClient();
                    var task = client.GetStringAsync($"https://update.knx.org/data/XML/project-{ns}/knx_master.xml");
                    while (!task.IsCompleted) { }
                    File.WriteAllText(Path.Combine(localPath, "Masters", $"project-{ns}.xml"), task.Result.ToString());
                }

                File.Copy(Path.Combine(localPath, "Masters", $"project-{ns}.xml"), Path.Combine(localPath, "Temp", $"knx_master.xml"));
                if (File.Exists(iKnxprodFileName)) File.Delete(iKnxprodFileName);
                System.IO.Compression.ZipFile.CreateFromDirectory(Path.Combine(localPath, "Temp"), iKnxprodFileName);


                if (!iIsDebug)
                    Directory.Delete(Path.Combine(localPath, "Temp"), true);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Output of {0} successful", iKnxprodFileName);
                Console.ResetColor();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ETS-Error during knxprod creation:");
                Console.WriteLine(ex.ToString());
                return 1;
            }
        }

        public static bool ValidateXsd(string iWorkingDir, string iTempFileName, string iXmlFileName, string iXsdFileName, bool iAutoXsd)
        {
            // iDocument.Save("validationTest.xml");
            string lContent = File.ReadAllText(iTempFileName);
            XDocument lDocument = XDocument.Parse(lContent, LoadOptions.SetLineInfo);
            lDocument.Root.Attribute("oldxmlns")?.Remove();

            string lXmlFileName = Path.GetFullPath(iXmlFileName);
            bool lError = false;
            if (string.IsNullOrEmpty(iXsdFileName) && iAutoXsd)
            {
                // we try to find a schema in the xml document
                Regex lRegex = new Regex("<\\?xml-model.* href=\"(.*.xsd)\" ");
                Match lMatch = lRegex.Match(lContent);
                iXsdFileName = lMatch.Groups[1].Value;
                // in case of an -editor.xsd, we use the original xsd
                iXsdFileName = iXsdFileName.Replace("-editor.xsd", ".xsd");
                if (!Path.IsPathFullyQualified(iXsdFileName))
                {
                    iXsdFileName = Path.Combine(iWorkingDir, iXsdFileName);
                    iXsdFileName = Path.GetRelativePath(Directory.GetCurrentDirectory(), iXsdFileName);
                }
            }
            if (!string.IsNullOrEmpty(iXsdFileName))
            {
                Console.Write("Validation against {0}... ", iXsdFileName);
                if (File.Exists(iXsdFileName))
                {
                    XmlSchemaSet schemas = new XmlSchemaSet();
                    schemas.Add(null, iXsdFileName);

                    lDocument.Validate(schemas, (o, e) =>
                    {
                        if (!lError) Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"XML {e.Severity} in {lXmlFileName}({e.Exception.LineNumber},{e.Exception.LinePosition}): ");
                        Console.ResetColor();
                        Console.WriteLine($"{e.Message}");
                        lError = true;
                    });
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(lError ? "" : "OK");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("Xsd-File not found!");
                }
            }
            return lError;
        }

        private enum DocumentCategory
        {
            None,
            Catalog,
            Hardware,
            Application
        }

        private static DocumentCategory GetDocumentCategory(XElement iTranslationUnit)
        {
            DocumentCategory lCategory = DocumentCategory.None;
            string lId = iTranslationUnit.Attribute("RefId").Value;

            lId = lId.Substring(6);
            if (lId.StartsWith("_A-"))
                lCategory = DocumentCategory.Application;
            else if (lId.StartsWith("_CS-"))
                lCategory = DocumentCategory.Catalog;
            else if (lId.StartsWith("_H-") && lId.Contains("_CI-"))
                lCategory = DocumentCategory.Catalog;
            else if (lId.StartsWith("_H-") && lId.Contains("_P-"))
                lCategory = DocumentCategory.Hardware;
            else if (lId.StartsWith("_H-"))
                lCategory = DocumentCategory.Hardware;

            return lCategory;
        }

        private static void AddTranslationUnit(XElement iTranslationUnit, List<XElement> iLanguageList, string iNamespaceName)
        {
            // we assume, that here are adding just few TranslationUnits
            // get parent element (Language)
            XElement lSourceLanguage = iTranslationUnit.Parent;
            string lSourceLanguageId = lSourceLanguage.Attribute("Identifier").Value;
            XElement lTargetLanguage = iLanguageList.Elements("Child").FirstOrDefault(child => child.Attribute("Name").Value == lSourceLanguageId);
            if (lTargetLanguage == null)
            {
                // we create language element
                lTargetLanguage = new XElement(XName.Get("Language", iNamespaceName), new XAttribute("Identifier", lSourceLanguageId));
                iLanguageList.Add(lTargetLanguage);
            }
            iTranslationUnit.Remove();
            lTargetLanguage.Add(iTranslationUnit);
        }
    }
}