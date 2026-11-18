namespace FastLabel.Backend.Helpers;

public class NaturalStringHelpers : IComparer<string?>
{
    private static readonly Regex NumericRegex = new("([0-9]+)", RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var xParts = NumericRegex.Split(x.Replace(" ", ""));
        var yParts = NumericRegex.Split(y.Replace(" ", ""));

        for (int i = 0; i < Math.Min(xParts.Length, yParts.Length); i++)
        {
            var xPart = xParts[i];
            var yPart = yParts[i];

            if (xPart == yPart) continue;

            bool isXNumeric = long.TryParse(xPart, out var xVal);
            bool isYNumeric = long.TryParse(yPart, out var yVal);

            if (isXNumeric && isYNumeric)
            {
                int comp = xVal.CompareTo(yVal);
                if (comp != 0) return comp;
            }
            else
            {
                int comp = string.Compare(xPart, yPart, StringComparison.OrdinalIgnoreCase);
                if (comp != 0) return comp;
            }
        }

        return xParts.Length.CompareTo(yParts.Length);
    }
}