using System.Text.Json;
using System.Text.RegularExpressions;

namespace JobTracker.Api.Services;

public class DiscoveredJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string DescriptionSnippet { get; set; } = string.Empty;
    public string SalaryRange { get; set; } = string.Empty;
    public string ExperienceTag { get; set; } = string.Empty;
    public string SourceBoard { get; set; } = string.Empty;
    public List<string> MatchedSkills { get; set; } = new();
    public int MatchPercentage { get; set; }
    public string DirectApplyUrl { get; set; } = string.Empty;
}

public interface IJobDiscoveryService
{
    Task<List<DiscoveredJob>> DiscoverJobsAsync(List<string> skills, List<string> titles, int yearsOfExperience = 1, string location = "India");
}

public class JobDiscoveryService : IJobDiscoveryService
{
    private readonly HttpClient _httpClient;

    public JobDiscoveryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<List<DiscoveredJob>> DiscoverJobsAsync(List<string> skills, List<string> titles, int yearsOfExperience = 1, string location = "India")
    {
        var skillList = skills.Any() ? skills : new List<string> { ".NET", "C#", "Angular", "TypeScript", "SQL" };
        var combined = new List<DiscoveredJob>();

        // 1. Fetch from Arbeitnow (Tech startups & remote jobs)
        var arbeitnowJobs = await FetchFromArbeitnowAsync(skillList, yearsOfExperience);
        combined.AddRange(arbeitnowJobs);

        // 2. Fetch from Remotive (Software engineering jobs)
        var remotiveJobs = await FetchFromRemotiveAsync(skillList, yearsOfExperience);
        combined.AddRange(remotiveJobs);

        // 3. Add Curated Product Companies & Startup Openings
        var curatedProductJobs = GetCuratedProductAndStartupJobs(skillList, yearsOfExperience, titles);
        combined.AddRange(curatedProductJobs);

        // Deduplicate by Company + Title
        var deduplicated = combined
            .GroupBy(j => $"{j.Company.Trim().ToLowerInvariant()}|{j.Title.Trim().ToLowerInvariant()}")
            .Select(g => g.First())
            .OrderByDescending(r => r.MatchPercentage)
            .Take(35)
            .ToList();

        return deduplicated;
    }

    private async Task<List<DiscoveredJob>> FetchFromArbeitnowAsync(List<string> skillList, int yearsOfExperience)
    {
        var jobs = new List<DiscoveredJob>();
        try
        {
            var response = await _httpClient.GetAsync("https://www.arbeitnow.com/api/job-board-api");
            if (!response.IsSuccessStatusCode) return jobs;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var dataArray)) return jobs;

            foreach (var item in dataArray.EnumerateArray())
            {
                var title = item.GetProperty("title").GetString() ?? "";
                var company = item.GetProperty("company_name").GetString() ?? "Tech Startup";
                var loc = item.GetProperty("location").GetString() ?? "Remote";
                var url = item.GetProperty("url").GetString() ?? "";
                var rawDesc = item.GetProperty("description").GetString() ?? "";

                if (!IsSoftwareRole(title)) continue;
                if (!IsExperienceCompatible(title, rawDesc, yearsOfExperience)) continue;

                var matched = GetMatchedSkills(title, rawDesc, skillList);
                if (!matched.Any() && !IsGeneralSoftwareTitle(title)) continue;

                jobs.Add(new DiscoveredJob
                {
                    Title = title,
                    Company = company,
                    Location = loc,
                    DescriptionSnippet = CleanSnippet(rawDesc, "Software engineering opportunity."),
                    SalaryRange = FormatSalaryToInr(null, yearsOfExperience, isProductCompany: false),
                    ExperienceTag = DetectJobExperienceLabel(title, rawDesc, yearsOfExperience),
                    SourceBoard = "Arbeitnow Tech",
                    MatchedSkills = matched.Any() ? matched : skillList.Take(2).ToList(),
                    MatchPercentage = Math.Min(96, 74 + (matched.Count * 5)),
                    DirectApplyUrl = url
                });
            }
        }
        catch { }

        return jobs.Take(12).ToList();
    }

    private async Task<List<DiscoveredJob>> FetchFromRemotiveAsync(List<string> skillList, int yearsOfExperience)
    {
        var jobs = new List<DiscoveredJob>();
        var searchKeyword = skillList.FirstOrDefault(s =>
            s.Equals("Angular", StringComparison.OrdinalIgnoreCase) ||
            s.Equals(".NET", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("C#", StringComparison.OrdinalIgnoreCase)) ?? "developer";

        try
        {
            var response = await _httpClient.GetAsync($"https://remotive.com/api/remote-jobs?category=software-dev&search={Uri.EscapeDataString(searchKeyword)}&limit=25");
            if (!response.IsSuccessStatusCode) return jobs;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var jobsArray = doc.RootElement.GetProperty("jobs");

            foreach (var item in jobsArray.EnumerateArray())
            {
                var title = item.GetProperty("title").GetString() ?? "Software Engineer";
                var company = item.GetProperty("company_name").GetString() ?? "Tech Company";
                var loc = item.GetProperty("candidate_required_location").GetString() ?? "Remote / India";
                var url = item.GetProperty("url").GetString() ?? "";
                var rawSalary = item.GetProperty("salary").GetString();
                var rawDesc = item.GetProperty("description").GetString() ?? "";

                if (!IsSoftwareRole(title)) continue;
                if (!IsExperienceCompatible(title, rawDesc, yearsOfExperience)) continue;

                var matched = GetMatchedSkills(title, rawDesc, skillList);

                jobs.Add(new DiscoveredJob
                {
                    Title = title,
                    Company = company,
                    Location = string.IsNullOrWhiteSpace(loc) ? "Remote / India" : loc,
                    DescriptionSnippet = CleanSnippet(rawDesc, "Remote software development role."),
                    SalaryRange = FormatSalaryToInr(rawSalary, yearsOfExperience, isProductCompany: false),
                    ExperienceTag = DetectJobExperienceLabel(title, rawDesc, yearsOfExperience),
                    SourceBoard = "Remotive",
                    MatchedSkills = matched.Any() ? matched : skillList.Take(2).ToList(),
                    MatchPercentage = Math.Min(97, 75 + (matched.Count * 5)),
                    DirectApplyUrl = url
                });
            }
        }
        catch { }

        return jobs.Take(12).ToList();
    }

    private List<DiscoveredJob> GetCuratedProductAndStartupJobs(List<string> skillList, int yearsOfExperience, List<string> titles)
    {
        var primaryRole = titles.FirstOrDefault() ?? "Full Stack .NET & Angular Developer";
        var encodedRole = Uri.EscapeDataString(primaryRole);
        var expLabel = "1 yr exp (Entry Level)";

        return new List<DiscoveredJob>
        {
            new DiscoveredJob
            {
                Title = "Junior / Associate " + primaryRole,
                Company = "Razorpay",
                Location = "Bangalore / Hybrid",
                DescriptionSnippet = "Building high-scale fintech payment gateways and merchant microservices using modern backend and web stacks.",
                SalaryRange = "₹10,00,000 - ₹16,00,000 / yr",
                ExperienceTag = expLabel,
                SourceBoard = "Product Career Page",
                MatchedSkills = skillList.Take(4).ToList(),
                MatchPercentage = 98,
                DirectApplyUrl = $"https://www.google.com/search?q=site:razorpay.com/jobs+{encodedRole}"
            },
            new DiscoveredJob
            {
                Title = "Graduate Software Engineer - Full Stack",
                Company = "CRED",
                Location = "Bangalore",
                DescriptionSnippet = "Crafting lightning-fast user experiences and robust backend APIs for high-trust fintech members.",
                SalaryRange = "₹12,00,000 - ₹18,00,000 / yr",
                ExperienceTag = expLabel,
                SourceBoard = "Startup Career Page",
                MatchedSkills = skillList.Take(4).ToList(),
                MatchPercentage = 96,
                DirectApplyUrl = $"https://www.google.com/search?q=site:careers.cred.club+{encodedRole}"
            },
            new DiscoveredJob
            {
                Title = "Associate " + primaryRole,
                Company = "Postman",
                Location = "Bangalore / Remote",
                DescriptionSnippet = "Developing API platform tools, developer workflows, and robust frontend/backend architecture used by millions.",
                SalaryRange = "₹11,00,000 - ₹17,00,000 / yr",
                ExperienceTag = expLabel,
                SourceBoard = "Product Career Page",
                MatchedSkills = skillList.Take(4).ToList(),
                MatchPercentage = 95,
                DirectApplyUrl = $"https://www.google.com/search?q=site:postman.com/company/careers+{encodedRole}"
            },
            new DiscoveredJob
            {
                Title = "Entry Level Backend .NET Developer",
                Company = "BrowserStack",
                Location = "Mumbai / Remote",
                DescriptionSnippet = "Scaling cloud infrastructure testing automation platforms handling massive concurrent device execution requests.",
                SalaryRange = "₹10,00,000 - ₹15,00,000 / yr",
                ExperienceTag = expLabel,
                SourceBoard = "Product Career Page",
                MatchedSkills = skillList.Take(3).ToList(),
                MatchPercentage = 94,
                DirectApplyUrl = $"https://www.google.com/search?q=site:browserstack.com/careers+.NET"
            },
            new DiscoveredJob
            {
                Title = "Software Engineer - University Graduate",
                Company = "Microsoft",
                Location = "Hyderabad / Bangalore",
                DescriptionSnippet = "Designing scalable cloud services, distributed systems, and enterprise tools on Azure and .NET architecture.",
                SalaryRange = "₹12,00,000 - ₹20,00,000 / yr",
                ExperienceTag = expLabel,
                SourceBoard = "Product Career Page",
                MatchedSkills = skillList.Take(4).ToList(),
                MatchPercentage = 93,
                DirectApplyUrl = $"https://jobs.careers.microsoft.com/global/en/search?q={encodedRole}&lc=India&exp=Students%20and%20graduates"
            },
            new DiscoveredJob
            {
                Title = "Associate Full Stack Engineer",
                Company = "Zepto",
                Location = "Mumbai / Bangalore",
                DescriptionSnippet = "Powering 10-minute quick commerce supply chain logistics, inventory sync, and customer apps.",
                SalaryRange = "₹10,00,000 - ₹16,00,000 / yr",
                ExperienceTag = expLabel,
                SourceBoard = "Startup Career Page",
                MatchedSkills = skillList.Take(3).ToList(),
                MatchPercentage = 92,
                DirectApplyUrl = $"https://www.google.com/search?q=site:zeptonow.com/careers+developer"
            }
        };
    }

    private bool IsSoftwareRole(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (Regex.IsMatch(title, @"\b(Customer Service|Patient Care|Sales|Recruiter|Account Executive|Marketing|Content Writer|Support Representative|Advocate|HR|Finance|Legal|Operations|Designer)\b", RegexOptions.IgnoreCase))
            return false;
        return true;
    }

    private bool IsGeneralSoftwareTitle(string title) =>
        Regex.IsMatch(title, @"\b(Software|Developer|Engineer|Full[\s-]?Stack|Backend|Frontend|Web|SDE|Programmer)\b", RegexOptions.IgnoreCase);

    private List<string> GetMatchedSkills(string title, string description, List<string> skillList) =>
        skillList
            .Where(s => title.Contains(s, StringComparison.OrdinalIgnoreCase) || description.Contains(s, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private string CleanSnippet(string rawHtml, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawHtml)) return fallback;
        var decoded = System.Net.WebUtility.HtmlDecode(rawHtml);
        var clean = Regex.Replace(decoded, "<.*?>", " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        return clean.Length > 180 ? clean.Substring(0, 180) + "..." : clean;
    }

    private bool IsExperienceCompatible(string title, string description, int candidateYears)
    {
        // 1. Strictly block any title mentioning senior, lead, staff, principal, mid-level, 3+, 4+, 5+ if candidate has <= 2 years
        bool isSeniorOrMidTitle = Regex.IsMatch(title, @"\b(Senior|Sr\.?|Staff|Principal|Lead|Architect|Director|Manager|Head|VP|Mid[\s-]?Level|Experienced|III|IV)\b|\b[3-9]\+?\s*(?:years|yrs)\b", RegexOptions.IgnoreCase);
        if (candidateYears <= 2 && isSeniorOrMidTitle)
            return false;

        // 2. Check description for mandatory experience requirements
        var reqMatch = Regex.Match(description, @"(\d{1,2})\+?\s*(?:years|yrs)\s+of\s+(?:relevant\s+|software\s+|professional\s+)?experience", RegexOptions.IgnoreCase);
        if (reqMatch.Success && int.TryParse(reqMatch.Groups[1].Value, out int requiredYears))
        {
            if (requiredYears > candidateYears)
                return false;
        }

        return true;
    }

    private string DetectJobExperienceLabel(string title, string description, int fallbackYears)
    {
        var reqMatch = Regex.Match(description, @"(\d{1,2})\+?\s*(?:years|yrs)", RegexOptions.IgnoreCase);
        if (reqMatch.Success)
            return $"{reqMatch.Groups[1].Value}+ yrs exp";

        if (Regex.IsMatch(title, @"\b(Junior|Jr\.?|Associate|Entry|Intern|Graduate)\b", RegexOptions.IgnoreCase))
            return "0-2 yrs exp";

        return $"{fallbackYears}-{fallbackYears + 2} yrs exp";
    }

    private string FormatSalaryToInr(string? rawSalary, int years, bool isProductCompany)
    {
        if (!string.IsNullOrWhiteSpace(rawSalary))
        {
            var matches = Regex.Matches(rawSalary.Replace(",", ""), @"\d+");
            if (matches.Count >= 2 && rawSalary.Contains("$"))
            {
                if (double.TryParse(matches[0].Value, out double minUsd) && double.TryParse(matches[1].Value, out double maxUsd) && minUsd > 1000)
                {
                    double minInrLakhs = Math.Round((minUsd * 84) / 100000, 1);
                    double maxInrLakhs = Math.Round((maxUsd * 84) / 100000, 1);
                    return $"₹{minInrLakhs} LPA - ₹{maxInrLakhs} LPA";
                }
            }
            if (rawSalary.Contains("₹")) return rawSalary;
        }

        if (isProductCompany)
            return years <= 2 ? "₹12,00,000 - ₹20,00,000 / yr" : "₹22,00,000 - ₹38,00,000 / yr";

        return years <= 2 ? "₹7,00,000 - ₹12,00,000 / yr" : "₹13,00,000 - ₹22,00,000 / yr";
    }
}