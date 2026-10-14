namespace Sagara.NuGetDiff.Tool.Packages;

/// <summary>
/// Compares strings ordinally, except that runs of ASCII digits are compared by numeric value, so that
/// "net8.0" sorts before "net10.0".
/// </summary>
/// <remarks>
/// <para>
/// We don't use <see cref="System.Globalization.CompareOptions.NumericOrdering"/> because .NET hands
/// culture-aware comparisons to a native library (ICU, or NLS on older Windows). In invariant globalization
/// mode, .NET doesn't load that library. That mode is common in slim containers and CI images, and is
/// often turned on to work around a missing libicu on Linux. Culture-aware comparisons then fall back to
/// ordinal, and NumericOrdering is silently ignored, so "net10.0" would sort before "net8.0".
/// </para>
/// <para>
/// Comparing ordinally also keeps the order identical across machines. Culture-aware sorting of punctuation
/// can differ slightly between ICU versions.
/// </para>
/// </remarks>
internal sealed class NumericOrdinalStringComparer : IComparer<string?>
{
    public static NumericOrdinalStringComparer Instance { get; } = new();

    private NumericOrdinalStringComparer()
    {
    }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int xIndex = 0;
        int yIndex = 0;

        while (xIndex < x.Length && yIndex < y.Length)
        {
            if (char.IsAsciiDigit(x[xIndex]) && char.IsAsciiDigit(y[yIndex]))
            {
                int digitRunComparison = CompareDigitRuns(ReadDigitRun(x, ref xIndex), ReadDigitRun(y, ref yIndex));
                if (digitRunComparison != 0)
                {
                    return digitRunComparison;
                }

                continue;
            }

            int charComparison = x[xIndex].CompareTo(y[yIndex]);
            if (charComparison != 0)
            {
                return charComparison;
            }

            xIndex++;
            yIndex++;
        }

        // One string is a prefix of the other; the shorter one sorts first.
        int remainingLengthComparison = (x.Length - xIndex).CompareTo(y.Length - yIndex);
        if (remainingLengthComparison != 0)
        {
            return remainingLengthComparison;
        }

        // The strings differ only in leading zeros (e.g., "net08.0" vs. "net8.0"). Fall back to ordinal so that
        //   only identical strings compare as equal.
        return string.CompareOrdinal(x, y);
    }


    //
    // Private methods
    //

    private static ReadOnlySpan<char> ReadDigitRun(string value, ref int index)
    {
        int start = index;

        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            index++;
        }

        return value.AsSpan(start, index - start);
    }

    private static int CompareDigitRuns(ReadOnlySpan<char> xDigits, ReadOnlySpan<char> yDigits)
    {
        // Compare without parsing so that arbitrarily long digit runs can't overflow: after trimming leading
        //   zeros, the longer run is the larger number, and runs of equal length compare digit by digit.
        xDigits = xDigits.TrimStart('0');
        yDigits = yDigits.TrimStart('0');

        int lengthComparison = xDigits.Length.CompareTo(yDigits.Length);
        if (lengthComparison != 0)
        {
            return lengthComparison;
        }

        return xDigits.SequenceCompareTo(yDigits);
    }
}
