using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace WordRush.Repository.Models;

public class Role: IdentityRole<int>
{
  public override int Id { get; set; }

  [Required]
  public override string Name { get; set; }

  public virtual ICollection<Privilege> Privileges { get; set; } = new List<Privilege>();

  public virtual ICollection<User> Users { get; set; } = new List<User>();
}
