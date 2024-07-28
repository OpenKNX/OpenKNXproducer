namespace OpenKNX.Toolbox.Lib.Data
{
    public class OpenKnxProject : IComparable
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public List<OpenKnxRelease> Releases { get; set; }

        public OpenKnxProject(long id, string name)
        {
            Id = id;
            Name = name;
            Releases = [];
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