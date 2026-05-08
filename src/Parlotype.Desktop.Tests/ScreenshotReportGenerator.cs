using System.Text;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// A step in a screenshot scenario: a description and a base64-encoded PNG image.
/// </summary>
public sealed record ScenarioStep(string Description, string Base64Png);

/// <summary>
/// A complete scenario with a name and a sequence of steps, each with a screenshot.
/// </summary>
public sealed record Scenario(string Name, string Summary, IReadOnlyList<ScenarioStep> Steps);

/// <summary>
/// Generates a self-contained HTML report from a collection of screenshot scenarios.
/// All images are embedded as base64 data URIs — no external files needed.
/// </summary>
public static class ScreenshotReportGenerator
{
    public static void Generate(string outputPath, string title, IReadOnlyList<Scenario> scenarios)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\" />");
        sb.AppendLine($"<title>{Escape(title)}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("""
            * { box-sizing: border-box; }
            body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 0; padding: 24px 40px; background: #f5f5f5; color: #1a1a1a; }
            h1 { font-size: 28px; margin-bottom: 8px; }
            .subtitle { color: #666; margin-bottom: 32px; font-size: 14px; }
            .scenario { background: #fff; border-radius: 12px; padding: 24px; margin-bottom: 24px; box-shadow: 0 1px 4px rgba(0,0,0,0.08); }
            .scenario h2 { font-size: 20px; margin: 0 0 4px; }
            .scenario .summary { color: #555; font-size: 14px; margin-bottom: 16px; }
            .step { display: flex; gap: 20px; align-items: flex-start; margin-bottom: 20px; padding-bottom: 20px; border-bottom: 1px solid #eee; }
            .step:last-child { border-bottom: none; margin-bottom: 0; padding-bottom: 0; }
            .step-number { flex-shrink: 0; width: 32px; height: 32px; border-radius: 50%; background: #0078d4; color: #fff; display: flex; align-items: center; justify-content: center; font-weight: 600; font-size: 14px; margin-top: 2px; }
            .step-content { flex: 1; }
            .step-desc { font-size: 14px; margin-bottom: 10px; line-height: 1.5; }
            .step-img { max-width: 100%; border: 1px solid #ddd; border-radius: 8px; }
        """);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"<h1>{Escape(title)}</h1>");
        sb.AppendLine($"<p class=\"subtitle\">Generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC &mdash; {scenarios.Count} scenario(s)</p>");

        foreach (var scenario in scenarios)
        {
            sb.AppendLine("<div class=\"scenario\">");
            sb.AppendLine($"<h2>{Escape(scenario.Name)}</h2>");
            sb.AppendLine($"<p class=\"summary\">{Escape(scenario.Summary)}</p>");

            for (var i = 0; i < scenario.Steps.Count; i++)
            {
                var step = scenario.Steps[i];
                sb.AppendLine("<div class=\"step\">");
                sb.AppendLine($"  <div class=\"step-number\">{i + 1}</div>");
                sb.AppendLine("  <div class=\"step-content\">");
                sb.AppendLine($"    <div class=\"step-desc\">{Escape(step.Description)}</div>");
                sb.AppendLine($"    <img class=\"step-img\" src=\"data:image/png;base64,{step.Base64Png}\" alt=\"Step {i + 1}\" />");
                sb.AppendLine("  </div>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(outputPath, sb.ToString());
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
