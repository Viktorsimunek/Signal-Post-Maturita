public partial class TerminalPowerController
{
    private static readonly string[][] TransmitKeyboardRows =
    {
        new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" },
        new[] { "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T" },
        new[] { "U", "V", "W", "X", "Y", "Z", "0", "1", "2", "3" },
        new[] { "4", "5", "6", "7", "8", "9", "-", ".", "/" },
        new[] { "SPACE", "BACK", "CLEAR", "NEXT" }
    };

    private static readonly string[][] PersonLookupKeyboardRows =
    {
        new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" },
        new[] { "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T" },
        new[] { "U", "V", "W", "X", "Y", "Z", "0", "1", "2", "3" },
        new[] { "4", "5", "6", "7", "8", "9", "-", ".", "/" },
        new[] { "BACK", "CLEAR", "SEARCH" }
    };
}
