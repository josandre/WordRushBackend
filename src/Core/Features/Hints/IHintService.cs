using System.Threading.Tasks;

namespace WordRush.Core.Features.Hints
{
  /// <summary>
  /// Defines the contract for hint services used in WordRush.  
  /// Responsible for generating helpful hints for a given category and starting letter
  /// using an AI model like Ollama.
  /// </summary>
  public interface IHintService
  {
    /// <summary>
    /// Requests a hint from the underlying AI model.
    /// </summary>
    /// <param name="letter">The starting letter of the answer.</param>
    /// <param name="category">The category of the answer.</param>
    /// <returns>A concise hint string, or <c>null</c> if no hint could be generated.</returns>
    Task<string?> GetHintAsync(string letter, string category);
  }
}