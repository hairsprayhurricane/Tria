namespace Tria.Models
{
    public class LearningBlock
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Key { get; set; } = null!;
        public string Description { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int Order { get; set; }
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Если true — у блока есть Unity-игра в Resources/GameContent/{Key}_Game/.
        /// Управляется через атрибут HasGame="true/false" в course.xml.
        /// </summary>
        public bool HasGame { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Module> Modules { get; set; } = new List<Module>();
    }
}
