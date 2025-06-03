namespace AuthService.Services.Interfaces
{
    public interface ITokenService
    {
        string GenerateJwtToken(int userId, string identifier);
        bool ValidateToken(string token);
        int? GetUserIdFromToken(string token);
    }
}
