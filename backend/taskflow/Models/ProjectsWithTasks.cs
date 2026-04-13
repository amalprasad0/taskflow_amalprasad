public class ProjectsWithTasks
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public List<Tasks>? Tasks { get; set; }
}