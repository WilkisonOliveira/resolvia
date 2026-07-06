namespace Resolvia.Application.DTOs.Category;

public class CategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DefaultSlaHours { get; set; }
}