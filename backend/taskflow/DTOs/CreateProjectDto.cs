using System.ComponentModel.DataAnnotations;

public class CreateProjectDto
{
    [Required(ErrorMessage = "Project name is required")]
    public string? ProjectName { get; set; }

    public string? ProjectDescription { get; set; }
}