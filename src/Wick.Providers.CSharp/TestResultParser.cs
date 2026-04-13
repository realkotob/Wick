using System.Globalization;
using System.Xml.Linq;

namespace Wick.Providers.CSharp;

/// <summary>
/// Parses .trx (Visual Studio Test Results) XML files into structured test results.
/// </summary>
public static class TestResultParser
{
    private static readonly XNamespace TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Parses a .trx file and returns structured test results.
    /// </summary>
    public static TestRunResult Parse(string trxContent)
    {
        var doc = XDocument.Parse(trxContent);
        var root = doc.Root!;

        var result = new TestRunResult();

        // ResultSummary
        var summary = root.Element(TrxNamespace + "ResultSummary");
        if (summary is not null)
        {
            result.Outcome = summary.Attribute("outcome")?.Value ?? "Unknown";
            var counters = summary.Element(TrxNamespace + "Counters");
            if (counters is not null)
            {
                result.Total = int.Parse(counters.Attribute("total")?.Value ?? "0", CultureInfo.InvariantCulture);
                result.Passed = int.Parse(counters.Attribute("passed")?.Value ?? "0", CultureInfo.InvariantCulture);
                result.Failed = int.Parse(counters.Attribute("failed")?.Value ?? "0", CultureInfo.InvariantCulture);
                result.Errors = int.Parse(counters.Attribute("error")?.Value ?? "0", CultureInfo.InvariantCulture);
            }
        }

        // Individual test results
        var results = root.Element(TrxNamespace + "Results");
        if (results is not null)
        {
            foreach (var unitResult in results.Elements(TrxNamespace + "UnitTestResult"))
            {
                var testResult = new TestResult
                {
                    TestName = unitResult.Attribute("testName")?.Value ?? "Unknown",
                    Outcome = unitResult.Attribute("outcome")?.Value ?? "Unknown",
                    Duration = unitResult.Attribute("duration")?.Value,
                };

                // Error info for failed tests
                var output = unitResult.Element(TrxNamespace + "Output");
                if (output is not null)
                {
                    var errorInfo = output.Element(TrxNamespace + "ErrorInfo");
                    if (errorInfo is not null)
                    {
                        testResult.ErrorMessage = errorInfo.Element(TrxNamespace + "Message")?.Value;
                        testResult.StackTrace = errorInfo.Element(TrxNamespace + "StackTrace")?.Value;
                    }

                    testResult.StdOut = output.Element(TrxNamespace + "StdOut")?.Value;
                }

                result.Tests.Add(testResult);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a .trx file from disk.
    /// </summary>
    public static TestRunResult ParseFile(string trxPath)
    {
        var content = File.ReadAllText(trxPath);
        return Parse(content);
    }
}

public sealed class TestRunResult
{
    public string Outcome { get; set; } = "Unknown";
    public int Total { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Errors { get; set; }
    public List<TestResult> Tests { get; } = [];
}

public sealed class TestResult
{
    public required string TestName { get; init; }
    public required string Outcome { get; init; }
    public string? Duration { get; init; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public string? StdOut { get; set; }
}
