using Microsoft.AspNetCore.Mvc;
using JobTracker.Api.Services;

namespace JobTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResumeController : ControllerBase
{
    private readonly IResumeParserService _parserService;
    private readonly IJobDiscoveryService _discoveryService;

    public ResumeController(IResumeParserService parserService, IJobDiscoveryService discoveryService)
    {
        _parserService = parserService;
        _discoveryService = discoveryService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadResume(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Please upload a valid PDF file.");

        using var stream = file.OpenReadStream();
        var parsed = _parserService.ParsePdf(stream);
        var feed = await _discoveryService.DiscoverJobsAsync(parsed.Skills, parsed.SuggestedTitles, parsed.YearsOfExperience);

        return Ok(new
        {
            skills = parsed.Skills,
            suggestedTitles = parsed.SuggestedTitles,
            yearsOfExperience = parsed.YearsOfExperience,
            experienceLevel = parsed.ExperienceLevel,
            feed
        });
    }
}