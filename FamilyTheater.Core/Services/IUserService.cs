using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyTheater.Core.Services
{
    public interface IUserService
    {
        Task<bool> ValidateCredentialsAsync(string userName, string password);
    }
}
