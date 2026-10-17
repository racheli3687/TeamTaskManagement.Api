using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeamTaskManagement.Core.Services
{
    public interface IPasswordHasher
    {
        // מחזיר hash עם salt מובנה
        string Hash(string password);

        // בודק סיסמה מול hash שנשמר
        bool Verify(string password, string passwordHash);
    }
}
