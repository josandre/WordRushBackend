namespace WordRush.Core.Features.Settings
{
  /// <summary>
  /// Defines the contract for category validation services used in WordRush
  /// Responsible for generating a response that defines if the speficied category
  /// is valid for a game or not using an AI model.
  /// </summary>
  public interface ICategoryValidationService
  {
    /// <summary>
    /// Requests if a category is valid for a game or not.
    /// </summary>
    /// <param name="categoryName">The name of the category.</param>
    /// <returns>A Json with a boolean and a category.</returns>
    Task<bool?> GetCategoryValidationAsync(string categoryName);
  }
}
