namespace Parlotype.Benchmark.Metrics;

/// <summary>Computes Word Error Rate (WER) and Character Error Rate (CER) using edit distance.</summary>
public static class EditDistanceCalculator
{
    /// <summary>
    /// Computes WER between reference and hypothesis text.
    /// Both texts are normalized before comparison.
    /// Returns a percentage (0.0 = perfect, 100.0 = all wrong).
    /// </summary>
    public static double ComputeWer(string referenceText, string hypothesisText)
    {
        var refNorm = TextNormalizer.Normalize(referenceText);
        var hypNorm = TextNormalizer.Normalize(hypothesisText);

        if (string.IsNullOrEmpty(refNorm))
            return string.IsNullOrEmpty(hypNorm) ? 0.0 : 100.0;

        var refTokens = refNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hypTokens = hypNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var (s, d, i, n) = ComputeEditOps(refTokens, hypTokens);
        return (double)(s + d + i) / n * 100.0;
    }

    /// <summary>
    /// Computes CER between reference and hypothesis text.
    /// Both texts are normalized before comparison.
    /// Returns a percentage (0.0 = perfect, 100.0 = all wrong).
    /// </summary>
    public static double ComputeCer(string referenceText, string hypothesisText)
    {
        var refNorm = TextNormalizer.Normalize(referenceText);
        var hypNorm = TextNormalizer.Normalize(hypothesisText);

        if (string.IsNullOrEmpty(refNorm))
            return string.IsNullOrEmpty(hypNorm) ? 0.0 : 100.0;

        var refChars = refNorm.ToCharArray().Select(c => c.ToString()).ToArray();
        var hypChars = hypNorm.ToCharArray().Select(c => c.ToString()).ToArray();

        var (s, d, i, n) = ComputeEditOps(refChars, hypChars);
        return (double)(s + d + i) / n * 100.0;
    }

    /// <summary>
    /// Computes edit operations (substitutions, deletions, insertions) between two sequences.
    /// Uses Wagner-Fischer DP algorithm.
    /// </summary>
    public static (int Substitutions, int Deletions, int Insertions, int ReferenceLength) ComputeEditOps(
        string[] reference, string[] hypothesis)
    {
        int n = reference.Length;
        int m = hypothesis.Length;

        if (n == 0)
            return (0, 0, m, 0);

        // dp[i,j] = min edit distance from reference[0..i-1] to hypothesis[0..j-1]
        var dp = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) dp[i, 0] = i;
        for (int j = 0; j <= m; j++) dp[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = string.Equals(reference[i - 1], hypothesis[j - 1], StringComparison.Ordinal) ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        // Backtrace to count S, D, I
        int si = n, sj = m;
        int subs = 0, dels = 0, ins = 0;
        while (si > 0 || sj > 0)
        {
            if (si > 0 && sj > 0 &&
                dp[si, sj] == dp[si - 1, sj - 1] + (string.Equals(reference[si - 1], hypothesis[sj - 1], StringComparison.Ordinal) ? 0 : 1))
            {
                if (!string.Equals(reference[si - 1], hypothesis[sj - 1], StringComparison.Ordinal))
                    subs++;
                si--; sj--;
            }
            else if (si > 0 && dp[si, sj] == dp[si - 1, sj] + 1)
            {
                dels++;
                si--;
            }
            else
            {
                ins++;
                sj--;
            }
        }

        return (subs, dels, ins, n);
    }
}
