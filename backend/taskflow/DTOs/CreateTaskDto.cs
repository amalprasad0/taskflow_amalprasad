using System.ComponentModel.DataAnnotations;

public class CreateTaskDto
{
    [Required(ErrorMessage = "Task title is required")]
    public string Title { get; set; } = null!;
    
    public string? Description { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "todo";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDateAt { get; set; } = DateTime.UtcNow;
}