using FamilyTheater.Core.Data;
using FamilyTheater.Core.Services;
using LoginWindow.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoginWindow.Models
{
    public class HomeWindowModel : ReactiveObject
    {
        IUserService _userService;
        public HomeWindowModel(IUserService userService)
        {
            _userService = userService;
        }

    }
}
