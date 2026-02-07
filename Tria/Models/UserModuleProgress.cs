namespace Tria.Models
{
    public class UserModuleProgress
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public int ModuleId { get; set; }
        public bool IsCompleted { get; set; } = false;
        public int? Score { get; set; }
        public int Attempts { get; set; } = 0;
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Module Module { get; set; } = null!;
    }
}
