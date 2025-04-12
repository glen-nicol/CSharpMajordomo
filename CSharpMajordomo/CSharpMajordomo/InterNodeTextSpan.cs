using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Threading;

namespace CSharpMajordomo;

public readonly struct InterNodeTextSpan(SourceText text, int firstNodeSpanEnd, int firstEndLine, int secondStartLine)
{
    public SourceText Text { get; } = text;

    public int FirstNodeSpanEnd { get; } = firstNodeSpanEnd;

    public int LineOfFirstEnd { get; } = firstEndLine;

    public int LineOfSecondStart { get; } = secondStartLine;

    public SourceText EditToHaveBlankLines(int targetLines, string endOfLine, CancellationToken cancel)
    {
        var maxLines = MaxContiguousBlankLines(cancel);
        var lines = Text.Lines;
        if(maxLines.ContiguousBlankLineCount > targetLines)
        {
            return Text.WithChanges(Enumerable.Range(maxLines.FirstLineIndex, maxLines.ContiguousBlankLineCount - targetLines).Select(i => new TextChange(lines[i].SpanIncludingLineBreak, string.Empty)));
        }

        if(maxLines.ContiguousBlankLineCount < targetLines)
        {
            return Text.WithChanges(new TextChange(new TextSpan(maxLines.InsertionStart, 0), string.Concat(Enumerable.Repeat(endOfLine, targetLines - maxLines.ContiguousBlankLineCount))));
        }

        return Text;

    }

    public MaxBlankLinesInfo MaxContiguousBlankLines(CancellationToken cancel)
    {
        // detect empty string and it does not count as a blank line
        var (text, lineOfPreviousEnd, lineOfCurrentStart) = (Text, LineOfFirstEnd, LineOfSecondStart);
        var lines = text.Lines;
        var maxStartSpanStart = 
            lineOfCurrentStart == lineOfPreviousEnd 
            ? FirstNodeSpanEnd 
            : lines[lineOfCurrentStart].Start;
        if (lineOfCurrentStart - lineOfPreviousEnd <= 1)
        {
            // -1 is used here to support adding 2 lines at once when two nodes are on the same line and need 2 lines to make 1 blank between them.
            return new(maxStartSpanStart, lineOfPreviousEnd, lineOfCurrentStart - lineOfPreviousEnd - 1);
        }


        var current = 0;
        var max = 0;
        var maxStartLine = lineOfPreviousEnd + 1;

        for (var i = lineOfPreviousEnd + 1; i < lineOfCurrentStart; i++)
        {
            cancel.ThrowIfCancellationRequested();
            if (SyntaxWhitespace.IsBlankOrWhitespace(lines[i]))
            {
                current++;
                if(current > max)
                {
                    maxStartLine = i;
                    max = current;
                }
            }
            else
            {
                current = 0;
            }
        }

        return new(maxStartSpanStart, maxStartLine, max);
    }
}
