namespace WordRush.Web.Features.Game.CategoryColumns.Models;

public class CategoryColumnsResponse
{
  public CategoryTypeDto CategoryType { get; set; } = new();
}

public class CategoryTypeDto
{
  public int Id { get; set; }
  public string Name { get; set; } = string.Empty;
  public List<CategoryColumnDto> CategoryColumns { get; set; } = new();
}

public class CategoryColumnDto
{
  public int Id { get; set; }
  public string Column { get; set; } = string.Empty;
}


