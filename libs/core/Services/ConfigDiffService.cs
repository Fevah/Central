namespace Central.Core.Services;

/// <summary>
/// LCS-based config diff — aligns old/new config lines for side-by-side display.
/// Extracted from MainWindow.xaml.cs BuildAlignedDiff.
/// </summary>
public static class ConfigDiffService
{
    public static void BuildAlignedDiff(string[] oldLines, string[] newLines,
        out string[] leftOut, out bool[] leftChanged,
        out string[] rightOut, out bool[] rightChanged)
    {
        int m = oldLines.Length, n = newLines.Length;
        var dp = new int[m + 1, n + 1];
        for (int i = m - 1; i >= 0; i--)
            for (int j = n - 1; j >= 0; j--)
                dp[i, j] = oldLines[i] == newLines[j]
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var left = new List<string>();
        var right = new List<string>();
        var lc = new List<bool>();
        var rc = new List<bool>();

        int oi = 0, ni = 0;
        while (oi < m && ni < n)
        {
            if (oldLines[oi] == newLines[ni])
            {
                left.Add(oldLines[oi]); lc.Add(false);
                right.Add(newLines[ni]); rc.Add(false);
                oi++; ni++;
            }
            else if (dp[oi + 1, ni] >= dp[oi, ni + 1])
            {
                left.Add(oldLines[oi]); lc.Add(true);
                right.Add(""); rc.Add(true);
                oi++;
            }
            else
            {
                left.Add(""); lc.Add(true);
                right.Add(newLines[ni]); rc.Add(true);
                ni++;
            }
        }
        while (oi < m) { left.Add(oldLines[oi]); lc.Add(true); right.Add(""); rc.Add(true); oi++; }
        while (ni < n) { left.Add(""); lc.Add(true); right.Add(newLines[ni]); rc.Add(true); ni++; }

        leftOut = left.ToArray();
        leftChanged = lc.ToArray();
        rightOut = right.ToArray();
        rightChanged = rc.ToArray();
    }
}
