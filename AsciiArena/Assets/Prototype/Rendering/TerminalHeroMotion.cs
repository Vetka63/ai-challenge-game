namespace AsciiArena
{
    // Authored ASCII poses preserve the existing glyph-based character and palette.
    public static class TerminalHeroMotion
    {
        private static readonly string[][] GlideA = {
            new[]{" /^\\  *", " (o) /|", "<|\\   |", " ~\\~  '"},
            new[]{"* /^\\  ", "|\\(#)  ", "| /|>  ", "' ~/~~ "},
            new[]{"* /^\\  ", "| <o|  ", "|<|\\_  ", "' ~\\~~ "},
            new[]{" /^\\  *", " |o>  |", " _/|> |", " ~~/~ '"}
        };
        private static readonly string[][] GlideB = {
            new[]{" /^\\  *", " (o)  |", " /|> /|", " ~/~~ '"},
            new[]{"* /^\\  ", "| (#)  ", "|<|\\ / ", "' ~~\\~ "},
            new[]{"* /^\\  ", "| <o|  ", "| /|>  ", "' ~~/~ "},
            new[]{" /^\\  *", " |o>  |", "<|\\  /|", " ~\\~~ '"}
        };
        private static readonly string[][] Dash = {
            new[]{" ~|~  *", " /\\  /|", " (o) / ", " /V\\  '"},
            new[]{"* /^\\  ", "| (#)  ", " \\V/  ", "' ||~  "},
            new[]{"* /\\__ ", "|<o|-- ", "| /V~~ ", "'  ~~  "},
            new[]{" __/\\ *", " --|o>|", " ~~V\\ |", "  ~~  '"}
        };
        private static readonly string[][] Hurt = {
            new[]{" /^\\  *", "\\(x)/ |", "  |  / ", " /V\\  '"},
            new[]{"* /^\\  ", "|\\(#)/ ", " \\|   ", "' /V\\  "},
            new[]{"* /^\\  ", "| <x|/ ", "| \\|  ", "' /V\\  "},
            new[]{" /^\\  *", "\\|x>  |", "  |/  |", " /V\\  '"}
        };
        public static string[] Get(HeroAnimationPlayer animation) => animation.Pose switch {
            "stepA" => GlideA[animation.Facing], "stepB" => GlideB[animation.Facing],
            "dash" => Dash[animation.Facing], "hurt" => Hurt[animation.Facing],
            _ => TerminalActorArt.Hero[animation.Facing] };
    }
}
