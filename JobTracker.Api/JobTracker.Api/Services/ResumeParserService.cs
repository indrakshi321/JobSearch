using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace JobTracker.Api.Services;

public class ParsedResumeData
{
    public string RawText { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
    public List<string> SuggestedTitles { get; set; } = new();
    public int YearsOfExperience { get; set; }
    public string ExperienceLevel { get; set; } = "Entry / Junior (1 yr)";
}

public interface IResumeParserService
{
    ParsedResumeData ParsePdf(Stream pdfStream);
}

public class ResumeParserService : IResumeParserService
{
    private static readonly string[] KnownTechSkills =
    {
        "C#", ".NET", "ASP.NET Core", "Entity Framework", "EF Core", "Web API",
        "Angular", "TypeScript", "JavaScript", "HTML5", "CSS3", "SCSS", "RxJS",
        "PrimeNG", "SQL Server", "PostgreSQL", "MySQL", "MongoDB", "Cosmos DB",
        "Azure", "AWS", "Docker", "Kubernetes", "Go", "Golang", "Python", "Java",
        "React", "Node.js", "REST", "Swagger", "Git", "CI/CD", "Microservices", "RAG"
    };

    public ParsedResumeData ParsePdf(Stream pdfStream)
    {
        var sb = new StringBuilder();
        using (var document = PdfDocument.Open(pdfStream))
        {
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }
        }

        string rawText = sb.ToString();

        var detectedSkills = KnownTechSkills
            .Where(skill => Regex.IsMatch(rawText, $@"\b{Regex.Escape(skill)}\b", RegexOptions.IgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int years = ExtractYearsOfExperience(rawText);
        string level = years <= 1 ? "Entry / Junior (1 yr)" : $"Mid-Level ({years} yrs)";

        var suggestedTitles = InferJobTitles(detectedSkills, years);

        return new ParsedResumeData
        {
            RawText = rawText,
            Skills = detectedSkills,
            SuggestedTitles = suggestedTitles,
            YearsOfExperience = years,
            ExperienceLevel = level
        };
    }

    private int ExtractYearsOfExperience(string text)
    {
        // 1. Look for explicit mentions like "1 year", "1 yr", "fresher", "entry level"
        if (Regex.IsMatch(text, @"\b(fresher|entry[\s-]?level|0\s*[-–to]\s*1\s*years?)\b", RegexOptions.IgnoreCase))
        {
            return 1;
        }

        var expMatch = Regex.Match(text, @"(\d{1,2})\+?\s*(?:years|yrs)(?:\s+of)?\s+(?:experience|exp)", RegexOptions.IgnoreCase);
        if (expMatch.Success && int.TryParse(expMatch.Groups[1].Value, out int explicitYears))
        {
            return Math.Clamp(explicitYears, 1, 25);
        }

        // 2. Target Graduation or Employment Year (e.g., Graduated 2025, Joined 2025)
        var gradMatch = Regex.Match(text, @"(?:graduated|graduation|b\.?tech|b\.?e\.?|degree|infosys).*?\b(202[4-6])\b", RegexOptions.IgnoreCase);
        int currentYear = DateTime.UtcNow.Year;

        if (gradMatch.Success && int.TryParse(gradMatch.Groups[1].Value, out int gradYear))
        {
            int calculatedExp = currentYear - gradYear;
            return Math.Clamp(calculatedExp <= 0 ? 1 : calculatedExp, 1, 15);
        }

        // 3. Fallback: check for any recent year like 2025
        var yearMatches = Regex.Matches(text, @"\b(202[0-6])\b");
        if (yearMatches.Count > 0)
        {
            int earliestYear = yearMatches.Select(m => int.Parse(m.Value)).Min();
            int diff = currentYear - earliestYear;
            if (diff >= 0 && diff <= 5) return Math.Max(1, diff);
        }

        return 1; // Default to 1 year for 2025 graduates
    }

    private List<string> InferJobTitles(List<string> skills, int years)
    {
        var titles = new List<string>();
        string prefix = years >= 5 ? "Senior " : (years <= 1 ? "Associate / Junior " : "");

        bool hasDotNet = skills.Any(s => s.Contains(".NET", StringComparison.OrdinalIgnoreCase) || s.Equals("C#", StringComparison.OrdinalIgnoreCase));
        bool hasAngular = skills.Any(s => s.Equals("Angular", StringComparison.OrdinalIgnoreCase));
        bool hasGo = skills.Any(s => s.Equals("Go", StringComparison.OrdinalIgnoreCase) || s.Equals("Golang", StringComparison.OrdinalIgnoreCase));

        if (hasDotNet && hasAngular)
            titles.Add($"{prefix}Full Stack .NET & Angular Developer");
        if (hasDotNet)
            titles.Add($"{prefix}.NET Core Backend Developer");
        if (hasAngular)
            titles.Add($"{prefix}Frontend Angular Developer");
        if (hasGo)
            titles.Add($"{prefix}Backend Go Engineer");

        if (!titles.Any())
            titles.Add($"{prefix}Software Engineer");

        return titles;
    }
}