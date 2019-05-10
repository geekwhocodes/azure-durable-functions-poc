using System;
using System.Collections.Generic;
using System.Text;

namespace EmployeeHiringFlow.Model
{
    public class JobApplication : Candidate
    {
        public JobApplication()
        {
            this.Description = "";
            this.Position = "Developer";
            this.Location = "Pune";
            this.ApplicationId = Guid.NewGuid();
        }
        public Guid ApplicationId { get; set; }
        public int JobId { get; set; }
        public string Position { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
    }

    public class InterviewRoundResult : FunctionInputBaseModel
    {
        public bool IsPassed { get; set; }
    }

    public class Offer : FunctionInputBaseModel
    {
        public bool Accepted { get; set; }
        public DateTime JoiningDate { get; set; }
    }
}
