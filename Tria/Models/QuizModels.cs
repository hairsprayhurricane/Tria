namespace Tria.Models;

public class QuizContent
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int PassScore { get; set; } = 80;
    public List<QuizQuestion> Questions { get; set; } = new();
}

public class QuizQuestion
{
    public string Text { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public int CorrectOptionIndex { get; set; }
}
