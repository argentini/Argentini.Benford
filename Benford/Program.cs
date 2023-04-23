using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Benford;

internal class Program
{
    private static double Benford(int z)
    {
        if (z is <= 0 or > 9)
        {
            // Ignore anything other than 1-9
            return 0;
        }

        // Calculate Benford's % for a given value from 1-9 (z)
        return Math.Log10(1 + 1 / (double)z);
    }

    private static StringBuilder GenerateOutput(Package package)
    {
        var output = new StringBuilder();
        var devs = new List<decimal>();
        var passed = false;
        decimal largestVariance = 0;

        output.Append("Digit   Benford [%]   Observed [%]   Deviation\r\n");
        output.Append("=====   ===========   ============   =========\r\n");

        for (var x = 0; x < 9; x++)
        {
            var temp = (decimal)(package.Data[x]/package.Total); // Calc % of total for x
            var ben = (decimal) Benford(x+1); // Calc Benford's value % for x
            var deviation = ben - temp;

            // GenerateOutput output that also includes standard deviation for Benford's value versus observed value
            var tempOutput = $"{(x + 1):0}       {ben * 100:00.00}         {temp * 100:00.00}          {deviation:0.000000}";

            devs.Add(Math.Abs(deviation));

            if (Math.Abs(deviation) > Math.Abs(largestVariance))
            {
                largestVariance = deviation;
            }

            output.Append(tempOutput + "\r\n");
        }

        if (Math.Abs(largestVariance) < (decimal)0.02)
        {
            if (devs.Average() < (decimal)0.007)
            {
                passed = true;
            }
        }

        output.Append("\r\n");
        output.Append($"LARGEST VARIANCE:                    {largestVariance:0.000000}\r\n");
        output.Append($"AVERAGE VARIANCE (ABS):              {devs.Average():0.000000}\r\n");
        output.Append($"RESULT:                              {(passed ? "PASSED (Probably Not Manipulated)" : "FAILED (Data Likely Manipulated)")}\r\n");

        output.Append("\r\n");

        return output;
    }

    private static async Task<Package> ProcessImageFile(string file)
    {
        var package = new Package(); // Use package so data can be passed to methods by ref

        using (var img = await Image.LoadAsync<Rgba32>(file))
        {
            img.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);

                    // pixelRow.Length has the same value as accessor.Width,
                    // but using pixelRow.Length allows the JIT to optimize away bounds checks:
                    for (var x = 0; x < pixelRow.Length; x++)
                    {
                        // Get a reference to the pixel at position x
                        ref var pixel = ref pixelRow[x];

                        // Convert pixel data into 32-bit RGBA value
                        var rgbaString = ((uint) ((pixel.R > 0 ? pixel.R : 1) * (pixel.G > 0 ? pixel.G : 1) *
                                               (pixel.B > 0 ? pixel.B : 1) * (pixel.A > 0 ? pixel.A : 1))).ToString();

                        // Get the first digit of the value
                        var val = rgbaString.Substring(0, 1);

                        //if (file.Contains("santa"))
                        //    await Console.Out.WriteLineAsync(_string);

                        // Increment the count for each first digit value (1-9)
                        switch (val)
                        {
                            case "1":
                                package.Data[0]++;
                                break;
                            case "2":
                                package.Data[1]++;
                                break;
                            case "3":
                                package.Data[2]++;
                                break;
                            case "4":
                                package.Data[3]++;
                                break;
                            case "5":
                                package.Data[4]++;
                                break;
                            case "6":
                                package.Data[5]++;
                                break;
                            case "7":
                                package.Data[6]++;
                                break;
                            case "8":
                                package.Data[7]++;
                                break;
                            case "9":
                                package.Data[8]++;
                                break;
                        }

                        // Count total pixels evaluated
                        package.Total++;
                    }
                }
            });
        }

        return package;
    }

    private static async Task Main()
    {
        var pathPrefix = "Benford";
        var depthCount = 1;

        while (Directory.Exists(pathPrefix) == false && depthCount < 10)
        {
            depthCount++;

            var paths = new string[depthCount];
            for (var x = 0; x < depthCount - 1; x++) paths[x] = "..";
            paths[depthCount - 1] = "Benford";
            pathPrefix = Path.Combine(paths);
        }

        if (Directory.Exists(pathPrefix))
        {
            var output = new StringBuilder();
            var output2 = new StringBuilder();
            var counter = 0;

            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("BENFORD ANALYSIS RUNNING...");
            await Console.Out.WriteLineAsync();

            var electionFiles = new [] { "2020-Biden.txt", "2020-Trump.txt" };
            
            #region Analyze 2020 Election Results
            
            foreach (var electionFile in electionFiles)
            {
                var list = await File.ReadAllLinesAsync(Path.Combine(pathPrefix, electionFile));
                var package = new Package();
                var outputText = new StringBuilder();

                outputText.Append($"Analyzing {electionFile} ({list.Length:0,000}" + " items)...\r\n---------------------------------------------------------------------------\r\n");

                foreach (var item in list.OrderBy(i => i))
                {
                    var val = item[..1];

                    switch (val)
                    {
                        case "1": package.Data[0]++; break;
                        case "2": package.Data[1]++; break;
                        case "3": package.Data[2]++; break;
                        case "4": package.Data[3]++; break;
                        case "5": package.Data[4]++; break;
                        case "6": package.Data[5]++; break;
                        case "7": package.Data[6]++; break;
                        case "8": package.Data[7]++; break;
                        case "9": package.Data[8]++; break;
                    }

                    package.Total++;
                }

                outputText.Append(GenerateOutput(package));

                output2.Append(outputText);

                await Console.Out.WriteLineAsync(outputText);
            }
            
            #endregion

            #region Analyze Images

            var jpegFiles = Directory.GetFiles(Path.Combine(pathPrefix, "images"), "*.jpg");
            var tiffFiles = Directory.GetFiles(Path.Combine(pathPrefix, "images"), "*.tiff");
            var files = jpegFiles.Concat(tiffFiles).ToList();

            foreach (var file in files.OrderBy(f => f))
            {
                var package = await ProcessImageFile(file);
                var fileText = file + $" ({package.Total:0,000} pixels)";
                var outputText = new StringBuilder();

                counter++;

                outputText.Append($"Image {counter} of {files.Count()}: {fileText.Split(Path.DirectorySeparatorChar).Last()}...\r\n---------------------------------------------------------------------------\r\n");
                outputText.Append(GenerateOutput(package));
                output.Append(outputText);

                await Console.Out.WriteLineAsync(outputText);
            }

            #endregion

            // Append image results to list.txt results
            output2.Append(output);

            // Write results to disk
            await File.WriteAllTextAsync(Path.Combine(pathPrefix, "output.txt"), output2.ToString());

            await Console.Out.WriteLineAsync("BENFORD ANALYSIS COMPLETE");
            await Console.Out.WriteLineAsync("The file 'output.txt' contains these results.");
            await Console.Out.WriteLineAsync();
        }

        else
        {
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("BENFORD ANALYSIS FAILED");
            await Console.Out.WriteLineAsync("Could not find the project directory. Run from Visual Studio or cd into the 'Benford' folder and use 'dotnet run'.");
            await Console.Out.WriteLineAsync();
        }
    }
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
