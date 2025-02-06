namespace WebApiFirstExample.Model
{
    public class ResumeAnalysis
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string ExtractedText { get; set; }
        public string LLMAnalysis { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
