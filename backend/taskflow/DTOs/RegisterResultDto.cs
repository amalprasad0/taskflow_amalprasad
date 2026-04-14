namespace taskFlow.DTOs
{
    public class RegisterResultDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
