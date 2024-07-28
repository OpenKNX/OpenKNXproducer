namespace OpenKNX.Toolbox.Lib.Data
{
    public class ReleaseContentFirmware
    {
        public string Name { get; set; }
        public string FilePathUf2 { get; set; }

        public ReleaseContentFirmware(string name, string filePathUf2)
        {
            Name = name;
            FilePathUf2 = filePathUf2;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}