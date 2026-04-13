using System.ComponentModel.DataAnnotations;

public class DeleteProjectDto{
    [Required(ErrorMessage = "ProjectId is required")]
    public Guid ProjectId { get; set; }
    [Required(ErrorMessage = "UserId is required")]
    public Guid UserId { get; set; }
}