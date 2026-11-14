using System.ComponentModel.DataAnnotations;

namespace JobTracker.Api.Models
{
    public enum AppStatus { Applied, Interview, Offer, Rejected, OnHold }

    public class JobApplication
    {
        [Key] public Guid Id { get; set; }
        [Required, MaxLength(120)] public string Company { get; set; } = "";
        [Required, MaxLength(120)] public string Role { get; set; } = "";
        public string? Location { get; set; }
        public string? Source { get; set; }
        [Required] public DateOnly DateApplied { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        [Required] public AppStatus Status { get; set; } = AppStatus.Applied;
        public string? Notes { get; set; }
    }
}
