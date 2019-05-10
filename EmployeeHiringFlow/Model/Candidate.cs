using System;
using System.Collections.Generic;
using System.Text;

namespace EmployeeHiringFlow.Model
{
    public class Candidate
    {
        public Candidate()
        {
            this.ResumeLink = "https://myawesomeresume.link";
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string ResumeLink { get; set; }
        public string Gender { get; set; }
    }
}
