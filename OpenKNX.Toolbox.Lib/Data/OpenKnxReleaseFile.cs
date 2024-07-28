namespace OpenKNX.Toolbox.Lib.Data
{
    public class OpenKnxReleaseFile : IComparable
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string DownloadUrl { get; set; }

        public OpenKnxReleaseFile(long id, string name, string downloadUrl)
        {
            Id = id;
            Name = name;
            DownloadUrl = downloadUrl;
        }

        public override string ToString()
        {
            return Name;
        }

        public int CompareTo(object? obj)
        {
            return string.Compare(ToString(), obj?.ToString(), StringComparison.CurrentCulture);
        }
    }
}