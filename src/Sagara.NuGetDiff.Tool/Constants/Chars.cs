namespace Sagara.NuGetDiff.Tool.Constants;

public static class Chars
{
    public const char UnicodeBOM = '\uFEFF';

    /// <summary>
    /// The ASCII single quote character, used to quote strings in PowerShell and POSIX shells.
    /// </summary>
    public const char SingleQuote = '\'';

    /// <summary>
    /// <para>The typographic left single quote character, which is treated as a quote character in PowerShell.</para>
    /// <para>Literal: <c>‘</c></para>
    /// </summary>
    public const char LeftSingleQuote = '\u2018';

    /// <summary>
    /// <para>The typographic right single quote character, which is treated as a quote character in PowerShell.</para>
    /// <para>Literal: <c>’</c></para>
    /// </summary>
    public const char RightSingleQuote = '\u2019';

    /// <summary>
    /// <para>The typographic single low-9 quote character, which is treated as a quote character in PowerShell.</para>
    /// <para>Literal: <c>‚</c></para>
    /// </summary>
    public const char SingleLow9Quote = '\u201A';

    /// <summary>
    /// <para>The typographic single high-reversed-9 quote character, which is treated as a quote character in PowerShell.</para>
    /// <para>Literal: <c>‛</c></para>
    /// </summary>
    public const char SingleHighReversed9Quote = '\u201B';
}
