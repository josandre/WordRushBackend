namespace WordRush.Core.Features.Realtime.Models
{
  [Serializable]
  internal class CheckValidCategoryResultEvent
  {
    public bool IsValidCategory { get; set; }
    public string Category { get; set; }
  }
}
