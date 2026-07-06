namespace Resolvia.Application.DTOs.Category;

public class CategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DefaultSlaHours { get; set; } = 24;
}