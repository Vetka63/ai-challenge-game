namespace AsciiArena
{
    // Original fixed-cell ASCII: south, north, west, east. No generated raster anatomy.
    public static class AsciiActorArt
    {
        public static readonly string[][] Idle = {
            new[] { "    /\\      ", "   /..\\  *  ", "   \\__/  |  ", "  /|::|\\ |  ", " ( |==| )|  ", "   /::\\  |  ", "  /|::|\\ |  ", " /_|__|_\\|  ", "   _  _     " },
            new[] { "    /\\      ", " * /==\\     ", " | \\==/     ", " |/|::|\\    ", " |(|==| )   ", " | /::\\     ", " |/|::|\\    ", " |_|__|_\\   ", "   _  _     " },
            new[] { "    /\\      ", " * <. |     ", " |  \\_|     ", " | /::|     ", " |()= |     ", " | /::|     ", " |/|::|     ", " |_|__|     ", "   _  _     " },
            new[] { "    /\\      ", "    | .> *  ", "    |_/  |  ", "    |::\\ |  ", "    | =()|  ", "    |::\\ |  ", "    |::|\\|  ", "    |__|_|  ", "   _  _     " }
        };
        public static readonly string[][][] Hover = BuildHover();
        public static readonly string[] Chaser = { "   /\\   ", "  /..\\  ", " <|==|> ", "  /::\\  ", " /::::\\ ", "  v~~v  " };
        public static readonly string[] Shooter = { "   /\\   ", "  /::\\  ", "  |..| *", " /|==|/ ", "< /::\\  ", " /::::\\ ", "  ~vv~  " };
        public static readonly string[] Heavy = { "  [====]  ", " /|o  o|\\ ", "< |====| >", " /| :: |\\ ", "| | :: | |", " /|::::|\\ ", "/_~v~~v~_\\" };
        public static readonly string[] Boss = { "  /\\  /\\  ", " /==\\/==\\ ", "[  O  O  ]", " \\======/ ", " /|::::|\\ ", "/ |====| \\", "| |::::| |", " /|::::|\\ ", "/_~v~~v~_\\" };
        private static string[][][] BuildHover()
        {
            // A continuous ragged hem, with no separate feet. Staff stays rigid.
            for (int d = 0; d < 4; d++)
                Idle[d][8] = d == 2 ? "   ~vv~     " : d == 3 ? "    ~vv~    " : "  ~v~~v~    ";
            var result = new string[4][][];
            for (int d = 0; d < 4; d++)
            {
                result[d] = new string[4][];
                for (int f = 0; f < 4; f++)
                {
                    result[d][f] = (string[])Idle[d].Clone();
                    if (f == 0 || f == 2)
                        result[d][f][8] = Idle[d][8].Replace('~', f == 0 ? '-' : '~').Replace('v', f == 0 ? '~' : 'v');
                }
            }
            return result;
        }
    }
}
