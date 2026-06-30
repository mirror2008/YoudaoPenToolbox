namespace YoudaoPenToolbox.Models
{
    public class ScreenMirrorDisplayInfo
    {
        public int Width { get; set; } = 936;
        public int Height { get; set; } = 280;
        public int TouchDirection { get; set; } = 270;

        public bool IsValid => Width > 0 && Height > 0;
    }
}
