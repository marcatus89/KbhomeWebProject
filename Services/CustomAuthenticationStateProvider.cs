using System;
using System.Threading.Tasks;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Authorization;
using System.Collections.Generic;
using System.Linq;

namespace DoAnTotNghiep.Services
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _js;
        private readonly ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        public CustomAuthenticationStateProvider(IJSRuntime js)
        {
            _js = js;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            string token = null;
            try
            {
                token = await _js.InvokeAsync<string>("localStorage.getItem", "jwt");
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthenticationState(_anonymous);
            }

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                if (jwt.ValidTo < DateTime.UtcNow)
                {
                    await LogoutAsync(); 
                    return new AuthenticationState(_anonymous);
                }

                var identity = new ClaimsIdentity(ParseClaimsFromJwt(jwt), "jwtAuth");
                var user = new ClaimsPrincipal(identity);
                return new AuthenticationState(user);
            }
            catch
            {
                await LogoutAsync();
                return new AuthenticationState(_anonymous);
            }
        }

        public void NotifyUserAuthentication(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var identity = new ClaimsIdentity(ParseClaimsFromJwt(jwt), "jwtAuth");
            var user = new ClaimsPrincipal(identity);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public async Task LogoutAsync()
        {
            try
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", "jwt");
            }
            catch { }

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
        }

        private IEnumerable<Claim> ParseClaimsFromJwt(JwtSecurityToken jwt)
        {
            var claims = new List<Claim>();
            var jwtClaims = jwt.Claims.ToList();

            
            var userIdClaim = jwtClaims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub) ??
                              jwtClaims.FirstOrDefault(c => c.Type == "nameid");
            if (userIdClaim != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userIdClaim.Value));
            }

            var emailClaim = jwtClaims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email) ??
                             jwtClaims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            if (emailClaim != null)
            {
                claims.Add(new Claim(ClaimTypes.Email, emailClaim.Value));
            }

            // Lấy Tên người dùng
            var nameClaim = jwtClaims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
            if (nameClaim != null)
            {
                claims.Add(new Claim(ClaimTypes.Name, nameClaim.Value));
            }

            var roleClaims = jwtClaims.Where(c => c.Type == "role" || c.Type == ClaimTypes.Role).ToList();
            foreach (var roleClaim in roleClaims)
            {
                claims.Add(new Claim(ClaimTypes.Role, roleClaim.Value));
            }

            return claims;
        }
    }
}
