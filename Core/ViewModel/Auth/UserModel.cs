using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainPersistence.Entities;

namespace Core.ViewModel.Auth
{
    public class UserModel
    {
        public string UserIdentifier { get; set; }
        public string Username { get; set; }
        public string EmailAddress { get; set; }
        public string ContactNumber { get; set; }
    }
}
