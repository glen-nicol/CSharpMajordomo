namespace CSharpMajordomo;

public readonly struct MaxBlankLinesInfo(int insertionStart, int lineIndex, int count)
{
    public int InsertionStart { get; } = insertionStart;

    public int FirstLineIndex { get; } = lineIndex;

    public int ContiguousBlankLineCount { get; } = count;
}