namespace Sagara.NuGetDiff.Tool.Packages;

/// <summary>
/// Compares strings ordinally, except that runs of ASCII digits are compared by numeric value, so that
/// "net8.0" sorts before "net10.0". Unlike <see cref="System.Globalization.CompareOptions.NumericOrdering"/>,
/// this doesn't depend on ICU, so it gives the same order in invariant globalization mode.
/// </summary>
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
