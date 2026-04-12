public class GetTaskByIdDto
{
    public Guid TaskId { get; set; }


    public Guid? Assignee { get; set; }   
    public string? Status { get; set; }   
}