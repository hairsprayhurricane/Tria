using System.ComponentModel.DataAnnotations;

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

        /// Прошёл ли пользователь игру (только для модулей типа Game).
        /// false пока игра не завершена.
        public bool IsGameCompleted { get; set; } = false;

        /// Результат прохождения игры в процентах от 0 до 100.
        /// Всегда 0 если IsGameCompleted = false.
        [Range(0, 100)]
        public int GameScore { get; set; } = 0;

        public Module Module { get; set; } = null!;
    }
}
