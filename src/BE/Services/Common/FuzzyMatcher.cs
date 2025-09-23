namespace Chats.BE.Services.Common;

using System;
using System.Collections.Generic;

public class FuzzyMatcher
{
    // 主函数：找到最佳匹配
    public static string FindBestMatch(string input, HashSet<string> options)
    {
        string? bestMatch = null;
        int bestDistance = int.MaxValue;

        foreach (string option in options)
        {
            int distance = LevenshteinDistance(input, option);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestMatch = option;
            }
        }

        return bestMatch!;
    }

    // 计算 Levenshtein 距离
    private static int LevenshteinDistance(string s1, string s2)
    {
        int len1 = s1.Length;
        int len2 = s2.Length;

        int[,] dp = new int[len1 + 1, len2 + 1];

        for (int i = 0; i <= len1; i++)
        {
            for (int j = 0; j <= len2; j++)
            {
                if (i == 0)
                {
                    dp[i, j] = j;
                }
                else if (j == 0)
                {
                    dp[i, j] = i;
                }
                else
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    dp[i, j] = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost
                    );
                }
            }
        }

        return dp[len1, len2];
    }
}
