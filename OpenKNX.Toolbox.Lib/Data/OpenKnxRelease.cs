namespace OpenKNX.Toolbox.Lib.Data
{
    public class OpenKnxRelease : IComparable
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public List<OpenKnxReleaseFile> Files { get; set; }

        public OpenKnxRelease(long id, string name)
        {
            Id = id;
            Name = name;
            Files = [];
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