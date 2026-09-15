namespace RiftVault.Models
{
    public class FileTag
    {
        public string Name { get; set; } = string.Empty;
        public string HexColor { get; set; } = "#FFFFFF";

        public FileTag() { }

        public FileTag(string name, string hexColor)
        {
            Name = name;
            HexColor = hexColor;
        }
    }
}