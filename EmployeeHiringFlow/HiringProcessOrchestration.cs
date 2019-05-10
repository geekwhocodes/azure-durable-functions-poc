using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EmployeeHiringFlow.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EmployeeHiringFlow
{
    public static class HiringProcessOrchestration
    {
        [FunctionName("HiringProcessOrchestrator")]
        public static async Task HiringProcessOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context, ILogger logger)
        {
            JobApplication jobApplication = context.GetInput<JobApplication>();

            ProcessInterviewRoundInputModel processInterviewRoundOne = new ProcessInterviewRoundInputModel()
            {
                JobApplication = jobApplication,
                RoundNumber = 1,
                OrchestratorInstanceId = context.InstanceId
            };
            InterviewRoundResult IsRoundOnePassed = await context.CallSubOrchestratorAsync<InterviewRoundResult>("InterviewRoundOrchestrator", processInterviewRoundOne);
             

            if (IsRoundOnePassed.IsPassed)
            {

                logger.LogWarning($"{jobApplication.Name} has passed round one, scheduling round {processInterviewRoundOne.RoundNumber + 1}.");

                processInterviewRoundOne.RoundNumber += 1;
                InterviewRoundResult IsRoundTwoPaased = await context.CallSubOrchestratorAsync<InterviewRoundResult>("InterviewRoundOrchestrator", processInterviewRoundOne);

                if (IsRoundTwoPaased.IsPassed)
                {
                    /// Rollout offer 
                    /// 
                    await context.CallActivityAsync<Notification>("RollOutOffer", processInterviewRoundOne.JobApplication);
                    logger.LogWarning($"Offer rolled out to {processInterviewRoundOne.JobApplication.Name}.");

                    /// Wait for offer approval from candidate
                    /// External Event
                    bool IsOfferAccepted = await context.CallSubOrchestratorAsync<bool>("OfferOrchestrator", processInterviewRoundOne);

                    if (IsOfferAccepted)
                    {
                        /// Fan out processing 
                        /// 
                        bool finalResult = await context.CallSubOrchestratorAsync<bool>("ReadinessOrchestrator", processInterviewRoundOne.JobApplication);
                        if (finalResult)
                        {
                            logger.LogWarning($"Everything is ready for {processInterviewRoundOne.JobApplication.Name} {finalResult}");
                        }
                        else
                        {
                            logger.LogWarning($"Not all things are ready for {processInterviewRoundOne.JobApplication.Name} {finalResult}");
                        }
                    }

                }
            }
            await context.CallActivityAsync<Notification>("DeclineApplication", processInterviewRoundOne.JobApplication);
        }

        [FunctionName("HiringProcessInit")]
        public static async Task HiringProcessInit(
            [QueueTrigger("jobapplications", Connection = "AzureWebJobsStorage")] JobApplication jobApplication,
            [OrchestrationClient] DurableOrchestrationClient durableOrchestrationStarter,
            ILogger log)
        {
            log.LogWarning(JsonConvert.SerializeObject(jobApplication));
            // Function input comes from the request content.
            string instanceId = await durableOrchestrationStarter.StartNewAsync("HiringProcessOrchestrator", jobApplication);
            
            log.LogWarning($"Started application processing orchestrator with Id = {instanceId}.");
        }


        #region HR Department Tasks

        
        [FunctionName("SubmitInterviewRoundResult")]
        public static async Task SubmitInterviewRoundResult(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "hr/interview")] HttpRequestMessage request,
            [OrchestrationClient] DurableOrchestrationClient durableOrchestrationClient,
            ILogger log)
        {

            string requestBody = await request.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(requestBody))
                throw new NullReferenceException();


            InterviewRoundResult data = JsonConvert.DeserializeObject<InterviewRoundResult>(requestBody);

            await durableOrchestrationClient.RaiseEventAsync(data.OrchestratorInstanceId, "InterviewRoundResultSubmitted", data);

        }


        [FunctionName("RollOutOffer")]
        [return: Queue("email-notifications", Connection = "AzureWebJobsStorage")]
        public static Notification RollOutOffer([ActivityTrigger] DurableActivityContext durableActivityContext,
            ILogger logger)
        {
            JobApplication candidateApplication = durableActivityContext.GetInput<JobApplication>();
            if (candidateApplication == null)
            {
                throw new NullReferenceException();
            }

            logger.LogWarning($"Rolling out offer to {candidateApplication.Name}.");

            Notification offerNotification = new Notification()
            {
                Subject = $"Offer to join as {candidateApplication.Position}",
                Message = $"Hi {candidateApplication.Name}, We are rolling out offer to you to join XYZ company.",
                To = candidateApplication.Email
            };

            return offerNotification;

        }

        [FunctionName("DeclineApplication")]
        [return: Queue("email-notifications", Connection = "AzureWebJobsStorage")]
        public static Notification DeclineApplication([ActivityTrigger] DurableActivityContext durableActivityContext,
            ILogger logger)
        {
            JobApplication candidateApplication = durableActivityContext.GetInput<JobApplication>();
            if (candidateApplication == null)
            {
                throw new NullReferenceException();
            }

            logger.LogWarning($"Application declined");

            Notification offerNotification = new Notification()
            {
                Subject = $"Update : {candidateApplication.Position}",
                Message = $"Hi {candidateApplication.Name}, Thank you for applying for the position {candidateApplication.Position} at XYZ company. Unfortunately, you haven’t met the requirements for this position. To view additional career opportunities, visit https://durable.compnamy",
                To = candidateApplication.Email
            };

            return offerNotification;

        }


        [FunctionName("JobOfferWithdrawn")]
        [return: Queue("email-notifications", Connection = "AzureWebJobsStorage")]
        public static Notification JobOfferWithdrew([ActivityTrigger] DurableActivityContext durableActivityContext,
            ILogger logger)
        {
            JobApplication candidateApplication = durableActivityContext.GetInput<JobApplication>();
            if (candidateApplication == null)
            {
                throw new NullReferenceException();
            }

            logger.LogWarning($"Offer withdrawn..");

            Notification offerNotification = new Notification()
            {
                Subject = $"Update : {candidateApplication.Position} | Job Id {candidateApplication.JobId}",
                Message = $"Hi, Job offer has been withdrawn by {candidateApplication.Name}",
                To = "ganeshraskar@outlook.com"
            };

            return offerNotification;

        }


        #region Interview Schedule Add Process Orchestrator

        [FunctionName("InterviewRoundOrchestrator")]
        public static async Task<InterviewRoundResult> InterviewRoundOrchestrator([OrchestrationTrigger]
            DurableOrchestrationContext orchestrationContext, ILogger log)
        {
            ProcessInterviewRoundInputModel interviewKit = orchestrationContext.GetInput<ProcessInterviewRoundInputModel>();

            log.LogWarning($"Scheduled and conducted interview with {interviewKit.JobApplication.Name}.");

            log.LogWarning($"Round {interviewKit.RoundNumber} processed, please submit result using instance {orchestrationContext.InstanceId}");

            using (var roundTimeoutTicks = new CancellationTokenSource())
            {

                DateTime expireIn = orchestrationContext.CurrentUtcDateTime.AddSeconds(60);

                Task roundTimerTask = orchestrationContext.CreateTimer(expireIn, roundTimeoutTicks.Token);

                Task<InterviewRoundResult> roundTaskResult = orchestrationContext.WaitForExternalEvent<InterviewRoundResult>("InterviewRoundResultSubmitted");

                Task result = await Task.WhenAny(roundTaskResult, roundTimerTask);

                if (roundTaskResult == result)
                {
                    roundTimeoutTicks.Cancel();
                    return roundTaskResult.Result;
                }
                else
                {
                    await orchestrationContext.CallActivityAsync<Notification>("DeclineApplication", interviewKit.JobApplication);
                }
                return new InterviewRoundResult() { IsPassed = false};
            }
        }

        #endregion


        #region Candidate readiness orchestrator 

        [FunctionName("ReadinessOrchestrator")]
        public static async Task<bool> ReadinessOrchestrator([OrchestrationTrigger]
            DurableOrchestrationContext orchestrationContext, ILogger log)
        {
            ProcessInterviewRoundInputModel interviewKit = orchestrationContext.GetInput<ProcessInterviewRoundInputModel>();

            log.LogWarning($" >>>>>>>> ReadinessOrchestrator triggered.");

            /// Configure this timeout depending on joining date 
            /// Example joining date - 1 day.
            DateTime expireIn = orchestrationContext.CurrentUtcDateTime.AddSeconds(180);

            /// Wait from offer accepted -- joining date - 1 day
            await orchestrationContext.CreateTimer(expireIn, CancellationToken.None);


            /// Fan out


            List<Task<bool>> internalTasks = new List<Task<bool>>();

            internalTasks.Add(orchestrationContext.CallActivityAsync<bool>("GetWelcomeKitReady", interviewKit.JobApplication));
            internalTasks.Add(orchestrationContext.CallActivityAsync<bool>("GetAccountReady", interviewKit.JobApplication));
            internalTasks.Add(orchestrationContext.CallActivityAsync<bool>("GetMachineReady", interviewKit.JobApplication));
            internalTasks.Add(orchestrationContext.CallActivityAsync<bool>("GetProjectTeamReady", interviewKit.JobApplication));


            await Task.WhenAll(internalTasks.ToArray());

            return internalTasks.All(t => t.Result == true);
        }



        #endregion




        #endregion

        #region Candidate Tasks

        [FunctionName("OfferOrchestrator")]
        public static async Task<bool> OfferOrchestrator([OrchestrationTrigger]
            DurableOrchestrationContext orchestrationContext, ILogger log)
        {
            ProcessInterviewRoundInputModel interviewKit = orchestrationContext.GetInput<ProcessInterviewRoundInputModel>();

            log.LogWarning($" >>>>>>>> Offer rolled out waiting for response from candidate. Accept offer using instance {orchestrationContext.InstanceId}");

            using (var offerTimeoutTicks = new CancellationTokenSource())
            {

                DateTime expireIn = orchestrationContext.CurrentUtcDateTime.AddSeconds(120);

                Task offerTimerTask = orchestrationContext.CreateTimer(expireIn, offerTimeoutTicks.Token);

                Task<bool> offerTaskResult = orchestrationContext.WaitForExternalEvent<bool>("JobOfferAccepted");

                Task result = await Task.WhenAny(offerTaskResult, offerTimerTask);

                if (offerTaskResult == result)
                {
                    offerTimeoutTicks.Cancel();
                    return true;
                }
                else
                {
                    await orchestrationContext.CallActivityAsync<Notification>("JobOfferWithdrawn", interviewKit.JobApplication);
                }
                return false;
            }
        }

        [FunctionName("AcceptJobOffer")]
        public static async Task<IActionResult> AcceptJobOffer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "careers/application/accept")] HttpRequestMessage request,
            [OrchestrationClient] DurableOrchestrationClient durableOrchestrationClient,
            ILogger log)
        {

            string requestBody = await request.Content.ReadAsStringAsync();

            if (string.IsNullOrEmpty(requestBody))
                return new BadRequestResult();


            Offer data = JsonConvert.DeserializeObject<Offer>(requestBody);

            await durableOrchestrationClient.RaiseEventAsync(data.OrchestratorInstanceId, "JobOfferAccepted", data.Accepted);

            return new NoContentResult();
        }


        #endregion

        #region Candidate Readiness

        [FunctionName("GetWelcomeKitReady")]
        public static async Task<bool> GetWelcomeKitReady([ActivityTrigger] DurableActivityContext context)
        {
            return true;
        }

        [FunctionName("GetAccountReady")]
        public static async Task<bool> GetAccountReady([ActivityTrigger] DurableActivityContext context)
        {
            return true;
        }

        [FunctionName("GetMachineReady")]
        public static async Task<bool> GetMachineReady([ActivityTrigger] DurableActivityContext context)
        {
            return true;
        }

        [FunctionName("GetProjectTeamReady")]
        public static async Task<bool> GetProjectTeamReady([ActivityTrigger] DurableActivityContext context)
        {
            return true;
        }

        #endregion




        #region ULT

        [FunctionName("GetStatus")]
        public static async Task<IActionResult> GetStatus(
            [OrchestrationClient] DurableOrchestrationClient client,
            [HttpTrigger(AuthorizationLevel.Anonymous, Route = "getStatus")] string instanceId, ILogger logger)
        {
            var status = await client.GetStatusAsync(instanceId);

            logger.LogWarning(JsonConvert.SerializeObject(status));

            return new OkObjectResult(status);
        }

        [FunctionName("RewindInstance")]
        public static Task RewindInstance(
        [OrchestrationClient] DurableOrchestrationClient client,
        [HttpTrigger(AuthorizationLevel.Anonymous, Route = "rewind-instance")] string instanceId, ILogger logger)
        {
            string reason = "Orchestrator failed and needs to be revived.";
            return client.RewindAsync(instanceId, reason);
        }

        #endregion
    }
}