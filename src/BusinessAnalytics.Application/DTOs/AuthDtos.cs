using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessAnalytics.Application.DTOs
{
    public class AuthDtos
    {
        public record LoginRequest(string Email, string Password);
        public record LoginResponse(string Token, DateTime ExpiresUtc);
        public record RegisterRequest(string Email, string Password, string DisplayName, string Role);
    }
}
