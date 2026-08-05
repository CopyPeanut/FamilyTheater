using FamilyTheater.Core.Data;
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
        public readonly AppDbContext _dbContext;
        public HomeWindowModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            
        }

    }
}
