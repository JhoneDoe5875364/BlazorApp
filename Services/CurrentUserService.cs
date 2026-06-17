using System.Security.Claims;
using HCP.HRPortal.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace HCP.HRPortal.Services;

/// <summary>
/// Resolves the <see cref="Employee"/> profile for the currently signed-in user by
/// matching their Identity login (UserName/email) to an employee record. Scoped, so it
/// caches the lookup for the lifetime of the Blazor circuit.
/// </summary>
public class CurrentUserService
{
    private readonly AuthenticationStateProvider _authState;
    private readonly IEmployeeService _employees;
    private Employee? _cached;
    private bool _resolved;

    public CurrentUserService(AuthenticationStateProvider authState, IEmployeeService employees)
    {
        _authState = authState;
        _employees = employees;
    }

    /// <summary>The signed-in employee, or <c>null</c> if not authenticated / not linked.</summary>
    public async Task<Employee?> GetEmployeeAsync()
    {
        if (_resolved) return _cached;

        var state = await _authState.GetAuthenticationStateAsync();
        var user = state.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var email = user.FindFirst(ClaimTypes.Email)?.Value
                        ?? user.FindFirst(ClaimTypes.Name)?.Value
                        ?? user.Identity.Name;

            if (!string.IsNullOrWhiteSpace(email))
                _cached = _employees.GetByEmail(email);
        }

        _resolved = true;
        return _cached;
    }
}
