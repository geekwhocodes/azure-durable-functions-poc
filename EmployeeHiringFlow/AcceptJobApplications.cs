using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using EmployeeHiringFlow.Model;

namespace EmployeeHiringFlow
{
    public static class AcceptJobApplications
    {

        /// <summary>
        /// Accepts new job applications
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>

        [FunctionName("AcceptJobApplications")]
        [return: Queue("jobapplications", Connection = "AzureWebJobsStorage")]
        public static async Task<Candidate> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "careers/apply")]
            HttpRequestMessage request,
            ILogger log)
        {

            try
            {
                string inputString = await request.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(inputString))
                {
                    throw new ArgumentNullException("Application is not valid.");
                }

                Candidate candidate = JsonConvert.DeserializeObject<Candidate>(inputString);

                log.LogWarning($"Application received : {JsonConvert.SerializeObject(candidate)}");

                return candidate;
            }
            catch(Exception e)
            {
                throw;
            }
        }
    }
}
