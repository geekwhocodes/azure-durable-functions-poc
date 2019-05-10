using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EmployeeHiringFlow.Model
{
    public class FunctionInputBaseModel
    {
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public string OrchestratorInstanceId { get; set; }
    }

    public class FunctionOutputBaseModel
    {
        public FunctionOutputBaseModel()
        {
            IsFaulted = false;
            Errors = new List<Error>();
        }
        public bool IsFaulted { get; set; }
        public List<Error> Errors { get; set; }
    }

    public class Error
    {
        public string DisplayMessage { get; set; }
        public Exception Exception { get; set; }
    }



    #region Job Application Models

    public class ProcessInterviewRoundInputModel : FunctionInputBaseModel
    {
        public int RoundNumber { get; set; }
        public JobApplication JobApplication { get; set; }
    }
    public class ProcessInterviewRoundOutputModel : FunctionOutputBaseModel
    {
        public bool IsPassed { get; set; }
    }



    #endregion
}
