using DataLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Helper
{
    public static class AvatarHelper
    {
        private const string Base = "https://avatar.iran.liara.run/public";

        public static string ForStudent() => Base; 
        public static string ForParent() => Base; 

        // Tutor: phân theo giới tính
        public static string ForTutor(Gender? gender)
        {
            if (gender == Gender.Female) return $"{Base}/girl";
            if (gender == Gender.Male) return $"{Base}/boy";
            return Base;
        }
    }
}
