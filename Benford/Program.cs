using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable RedundantBoolCompare

namespace Benford;

internal static class Program
{
    private static async Task Main()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("BENFORD ANALYSIS RESULTS...");
        AnsiConsole.WriteLine();

        var outputText = new StringBuilder();
        
        await ProcessTextFilesAsync(["2020-Biden.txt", "2020-Trump.txt", "2020-Jorgensen.txt", "2020-Other.txt"], outputText, "2020");
        await ProcessTextFilesAsync(["2024-Harris.txt", "2024-Trump.txt", "2024-Oliver.txt", "2024-Other.txt"], outputText, "2024");
        
        await File.WriteAllTextAsync(Path.Combine(GetPathPrefix(), "output.txt"), outputText.ToString());

        AnsiConsole.WriteLine("The file 'output.txt' contains these results.");
        AnsiConsole.WriteLine();
    }
    
    #region Output Generation

    private static void WriteToStringBuilder(StringBuilder sb, Renderable renderable)
    {
        using var writer = new StringWriter(sb);
        
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,    
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer)
        });

        console.Write(renderable);
    }
    
    private static StringBuilder GenerateOutput(string fileName, Package package)
    {
        var output = new StringBuilder();
        var devs = new List<double>();
        var largestVariance = 0d;

        var table = new Table
        {
            Width = 80,
        };

        table.AddColumn(new TableColumn(fileName).Centered());
        
        AnsiConsole.Write(table);

        WriteToStringBuilder(output, table);
        
        table = new Table
        {
            Width = 80,
        };

        table.AddColumn(new TableColumn("[bold]Digit[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Benford %[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Observed %[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Deviation[/]").RightAligned());
        
        for (var x = 0; x < 9; x++)
        {
            var temp = (package.Data[x]/package.Total); // Calc % of total for x
            var ben = Benford(x+1); // Calc Benford's value % for x
            var deviation = ben - temp;

            table.AddRow($"{(x + 1):0}", $"{ben * 100:00.00}", $"{temp * 100:00.00}", $"{deviation:0.000000}");
            devs.Add(Math.Abs(deviation));

            if (Math.Abs(deviation) > Math.Abs(largestVariance))
                largestVariance = deviation;
        }

        AnsiConsole.Write(table);
        WriteToStringBuilder(output, table);
        
        var (chi2, pValue) = ChiSquare(package);
        var mad = devs.Average();
        var maxDevAbs = Math.Abs(largestVariance);

        // Scores
        var pScore   = PScore(pValue);
        var madScore = MadScore(mad);
        var maxScore = MaxDevScore(maxDevAbs);

        // Optional effect size (k = 9)
        var v = CramersV(chi2, package.Total, 9);
        var vScore = VScore(v);

        // PRACTICAL FIT (headline): MAD + V + MaxDev
        var practical = 100.0 * GeoMean(
            (madScore, 0.45),
            (vScore,   0.35),
            (maxScore, 0.20)
        );
        var practicalGrade = Grade(practical);

        // SIGNIFICANCE (diagnostic): p-value only (softened via PScore)
        var significance = 100.0 * GeoMean((pScore, 1.0));
        var significanceGrade = Grade(significance);

        // Existing Nigrini-style result by MAD
        var resultByMad = mad switch
        {
            <= 0.006 => "Close",
            <= 0.012 => "Acceptable",
            <= 0.015 => "Marginal",
            _ => "Non-Conforming"
        };

        table = new Table
        {
            Width = 80
        };

        table.AddColumn(new TableColumn("[bold]Metric[/]"));
        table.AddColumn(new TableColumn("[bold]Value[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Score[/]").RightAligned());

        table.AddRow("Total Data Points", $"{package.Total:0}");
        table.AddRow("Max Deviation", $"{largestVariance:0.000000}", $"{maxScore:0.000}");
        table.AddRow("Mean Absolute Deviation", $"{mad:0.000000}", $"{madScore:0.000}");
        table.AddRow("Cramér's V (effect size)", $"{v:0.000000}", $"{vScore:0.000}");
        table.AddRow("Chi-Square (df=8)", $"{chi2:0.000000}");
        table.AddRow("P-Value", $"{pValue:0.000000}", $"{pScore:0.000}");
        
        AnsiConsole.Write(table);
        WriteToStringBuilder(output, table);

        table = new Table
        {
            Width = 80,
            ShowRowSeparators = true,
            Caption = new TableTitle($"Scores are on a scale of 0-100; higher is better{Environment.NewLine}Grades are Close, Acceptable, Marginal, & Non-Conforming")
        };
        
        table.AddColumn(new TableColumn("[bold]Scoring[/]"));
        table.AddColumn(new TableColumn("[bold]Score[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Grade[/]").RightAligned());
        
        table.AddRow($"Practical Fit;{Environment.NewLine}[dim]primary score for conformity[/]", $"{practical:0.0} / 100", $"{practicalGrade}");
        //table.AddEmptyRow();
        table.AddRow($"Significance (χ²/p);{Environment.NewLine}[dim]deviations most likely by chance[/]", $"{significance:0.0} / 100", $"{significanceGrade}");
        //table.AddEmptyRow();
        table.AddRow($"MAD Grade;{Environment.NewLine}[dim]how closely dataset conforms[/]", string.Empty, $"{resultByMad}");
        
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        WriteToStringBuilder(output, table);

        output.Append(Environment.NewLine);
        output.Append(Environment.NewLine);
        
        return output;
    }

    #endregion
    
    #region Process Files
    
    private static int FirstSignificantDigit(string s)
    {
        return (from ch in s where ch is not ('0' or '+' or '-' or '.' or ',' or ' ') && char.IsWhiteSpace(ch) == false where ch is >= '1' and <= '9' select ch - '0').FirstOrDefault();
    }
    
    public static string GetPathPrefix()
    {
        var pathPrefix = "Benford";
        var depthCount = 1;

        while (Directory.Exists(pathPrefix) == false && depthCount < 10)
        {
            depthCount++;

            var paths = new string[depthCount];
            
            for (var x = 0; x < depthCount - 1; x++)
                paths[x] = "..";

            paths[depthCount - 1] = "Benford";
            pathPrefix = Path.Combine(paths);
        }
        
        return pathPrefix;
    }
    
    public static async Task ProcessTextFilesAsync(string[] fileNames, StringBuilder outputText, string category = "")
    {
        var pathPrefix = GetPathPrefix();

        if (Directory.Exists(pathPrefix) == false)
            return;
        
        var aggregateList = new List<string>();

        foreach (var electionFile in fileNames)
        {
            var list = (await File.ReadAllLinesAsync(Path.Combine(pathPrefix, electionFile)))
                .Where(line => string.IsNullOrWhiteSpace(line) == false && line != "0")
                .ToArray();
            var package = new Package();

            aggregateList.AddRange(list);
            
            foreach (var item in list)
            {
                var val = FirstSignificantDigit(item);

                if (val is < 1 or > 9)
                    continue;
                    
                package.Data[val - 1]++;
                package.Total++;
            }
            
            outputText.Append(GenerateOutput(electionFile, package));
        }
            
        var aggregatePackage = new Package();

        foreach (var val in aggregateList.Select(FirstSignificantDigit).Where(val => val is >= 1 and <= 9))
        {
            aggregatePackage.Data[val - 1]++;
            aggregatePackage.Total++;
        }

        outputText.Append(GenerateOutput($"{category} aggregate results...".Trim(), aggregatePackage));
    }
    
    #endregion
    
    #region Scoring Helpers
 
    private static double Benford(int z)
    {
        if (z is <= 0 or > 9)
            return 0;

        // Calculate Benford's % for a given value from 1-9 (z)
        return Math.Log10(1 + 1 / (double)z);
    }

    private static string Grade(double score) =>
        score >= 85 ? "Close" :
        score >= 70 ? "Acceptable" :
        score >= 55 ? "Marginal" : "Non-Conforming";
    
    private static (double Chi2, double PValue) ChiSquare(Package package)
    {
        var chi2 = 0.0;
        
        for (var d = 1; d <= 9; d++)
        {
            var expected = Benford(d) * package.Total;
            var observed = package.Data[d - 1];

            if (expected > 0)
                chi2 += Math.Pow(observed - expected, 2) / expected;
        }

        // Degrees of freedom = 8
        var pValue = ChiSquarePValue(chi2, 8);

        return (chi2, pValue);
    }
    
    private static double ChiSquarePValue(double chi2, int df)
    {
        // Regularized gamma function for chi-square tail probability
        // Equivalent to 1 - CDF
        return 1.0 - GammaLowerIncomplete(df / 2.0, chi2 / 2.0) / Gamma(df / 2.0);
    }

    private static double Gamma(double z)
    {
        // Gamma function via Stirling’s approximation
        return Math.Sqrt(2 * Math.PI / z) * Math.Pow(z / Math.E, z) * (1 + 1.0 / (12 * z) + 1.0 / (288 * z * z) - 139.0 / (51840.0 * Math.Pow(z, 3)));
    }

    private static double GammaLowerIncomplete(double s, double x)
    {
        var sum = 0d;
        var term = 1 / s;
        var k = 0;

        while (term > 1e-12)
        {
            sum += term;
            k++;
            term *= x / (s + k);
        }

        return Math.Pow(x, s) * Math.Exp(-x) * sum;
    }
    
    /// <summary>
    /// Clamp x to [0,1]
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    private static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);

    /// <summary>
    /// Linear interpolation, clamped to [0,1]
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="x"></param>
    /// <returns></returns>
    private static double InvLerpClamped(double a, double b, double x)
    {
        if (Math.Abs(a - b) <= double.Epsilon)
            return x >= b ? 1 : 0;

        return Clamp01((x - a) / (b - a));
    }
    
    /// <summary>
    /// MAD → score using Nigrini-ish bands
    /// </summary>
    /// <param name="mad"></param>
    /// <returns></returns>
    private static double MadScore(double mad)
    {
        return mad switch
        {
            <= 0.006 => 1.0,
            <= 0.012 => 1.0 - InvLerpClamped(0.006, 0.012, mad) * 0.35,
            <= 0.015 => 0.65 - InvLerpClamped(0.012, 0.015, mad) * 0.65,
            _ => 0.0
        };
    }

    /// <summary>
    /// Max deviation → score (cap "bad" at 10 percentage points)
    /// </summary>
    /// <param name="maxDev"></param>
    /// <returns></returns>
    private static double MaxDevScore(double maxDev) => 1.0 - Clamp01(maxDev / 0.10);

    /// <summary>
    /// p-value → score (full credit above 0.10; linear down to 0 at 0)
    /// </summary>
    /// <param name="pValue"></param>
    /// <returns></returns>
    private static double PScore(double pValue) => Clamp01(pValue / 0.10);

    /// <summary>
    /// Cramér's V (optional effect size)
    /// </summary>
    /// <param name="chi2"></param>
    /// <param name="n"></param>
    /// <param name="k"></param>
    /// <returns></returns>
    private static double CramersV(double chi2, double n, int k) => Math.Sqrt(chi2 / (n * (k - 1)));

    /// <summary>
    /// V → score (treat V >= 0.10 as large)
    /// </summary>
    /// <param name="v"></param>
    /// <returns></returns>
    private static double VScore(double v) => 1.0 - Clamp01(v / 0.10);

    /// <summary>
    /// Weighted geometric mean
    /// </summary>
    /// <param name="parts"></param>
    /// <returns></returns>
    private static double GeoMean(params (double value, double weight)[] parts)
    {
        var logSum = 0.0d;
        var wSum = 0.0d;

        foreach (var (v, w) in parts)
        {
            var vv = Clamp01(v);

            // avoid log(0): floor at tiny positive
            logSum += w * Math.Log(Math.Max(vv, 1e-12));
            wSum  += w;
        }
        
        return Math.Exp(logSum / Math.Max(wSum, 1e-12));
    }    
    
    #endregion
}

internal class Package
{
    public Package()
    {
        Clear();
    }

    public double[] Data { get; set; }

    public double Total { get; set; }

    public void Clear()
    {
        Total = 0;
        Data = new double[9];
    }
}
