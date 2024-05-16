using System.Text;

namespace OpenKNXproducer
{
    public static class PathHelper
    {
        // Resolve windows shortuct .lnk file target
        static string GetLnkTargetPath(string iFilepath)
        {
            var lLink = WindowsShortcutFactory.WindowsShortcut.Load(iFilepath);
            return Path.Combine(lLink.WorkingDirectory, lLink.Path);
        }

        // Build full path and resolve .lnk files in the path specification
        public static string BuildFullPath(string iCurrentDir, string iPath)
        {
            string lCurrentDir = Path.IsPathFullyQualified(iCurrentDir) ? iCurrentDir : Path.Combine(Directory.GetCurrentDirectory(), iCurrentDir);
            var lAbsolutePath = Path.GetFullPath(Path.Combine(lCurrentDir, iPath));
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return lAbsolutePath;
            // Windows only: check path and resolve .lnk file links
            var lDir = new StringBuilder(Path.GetPathRoot(lAbsolutePath));
            var lParts = lAbsolutePath.Substring(lDir.Length).Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar } );
            for (int i = 0; i < lParts.Length - 1; i++)
            {
                lDir.Append(lParts[i]);
                string lPathToCurrentSegment = lDir.ToString();
                if (!Directory.Exists(lPathToCurrentSegment))
                {
                    // Check if shortcut file .lnk exist on the path
                    string lLinkFile = lPathToCurrentSegment + ".lnk";
                    if (File.Exists(lLinkFile))
                    {
                        var lDirectoryOfLink = Path.GetDirectoryName(lPathToCurrentSegment);
                        string lTargetDir = Path.Combine(lDirectoryOfLink, GetLnkTargetPath(lLinkFile));
                        string lFullPath = lTargetDir + Path.DirectorySeparatorChar + string.Join(Path.DirectorySeparatorChar, lParts.Skip(i + 1));
                        return  lFullPath;
                    }
                    // part of path not found, us as it is
                    return lAbsolutePath;
                }
                lDir.Append(Path.DirectorySeparatorChar);
            }
            return lAbsolutePath;
        }
    }
}