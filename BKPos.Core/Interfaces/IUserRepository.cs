using BKPos.Core.Models;

namespace BKPos.Core.Interfaces;

public interface IUserRepository
{
    AppUser? Authenticate(string username, string password);

    IReadOnlyList<AppUser> GetAll();

    void AddUser(AppUser user, string plainPassword);

    void UpdateUser(AppUser user);

    void ChangePassword(int userId, string newPlainPassword);

    void DeleteUser(int userId);
}
