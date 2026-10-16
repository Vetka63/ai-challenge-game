namespace AsciiArena
{
    // Style A, extended to south/north/west/east without changing movement or collision shapes.
    public static class TerminalActorArt
    {
        public static readonly string[][] Hero={
            new[]{" /^\\  *"," (o)  |"," /|\\ / "," ~V~  '"},
            new[]{"* /^\\  ","| (#)  "," \\/|\\ ","' ~V~  "},
            new[]{"* /^\\  ","| <o|  ","| /|\\  ","' ~V~  "},
            new[]{" /^\\  *"," |o>  |"," /|\\  |"," ~V~  '"}
        };
        public static readonly string[] Chaser={" \\|/ ","-<x>-"," /|\\ "};
        public static readonly string[] Shooter={".----.","| >_ |","'--+-'"," _/\\_ "};
        public static readonly string[] Heavy={"[|||||]","|# # #|","[=====]"," _| |_ "};
        public static readonly string[] Boss={"   /^^^\\   ","  [O:#:O]  "," /[#####]\\ ","/##:###:##\\","  |:#:#:|  "," /##:#:##\\ "," ~V~~V~~V~ "};
        public static readonly string[] Tree={" .----. "," |--: | "," |--. | "," |--: | "," '----' "," _|  |_ "};
    }
}
