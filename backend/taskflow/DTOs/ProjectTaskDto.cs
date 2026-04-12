namespace taskFlow.DTOs
{
    public class ProjectTaskDto
    {
        public Guid Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid OwnerId { get; set; }
        
        public Guid? TaskId { get; set; }
        public string? TaskName { get; set; }
        public string? TaskDescription { get; set; }
        public string? TaskStatus { get; set; }
        public Guid? TaskAssigneeId { get; set; }
    }
}
