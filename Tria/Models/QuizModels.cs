namespace Tria.Models
{
    public class QuizQuestion
    {
        public int Id { get; set; }
        public string Text { get; set; } = null!;
        public List<string> Options { get; set; } = new();
        public int CorrectOptionIndex { get; set; }
        public string? Explanation { get; set; }
    }

    public class QuizContent
    {
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int PassScore { get; set; } = 80;
        public List<QuizQuestion> Questions { get; set; } = new();
    }
}
