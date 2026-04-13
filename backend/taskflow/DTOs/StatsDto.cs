public class StatsDto
{
    // ✅ Fixed: int values, not string — DB returns {"done": 1, "todo": 1}
    public Dictionary<string, int> TasksPerStatus { get; set; }
    public Dictionary<string, int> TasksPerPriority { get; set; }
    public Dictionary<string, int> TasksPerAssignee { get; set; }
}