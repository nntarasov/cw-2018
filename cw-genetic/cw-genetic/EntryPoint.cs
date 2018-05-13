using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Text;

namespace cw_genetic
{
    public class CwConfig
    {
        public CwApp[] Apps { get; set; }
        public CwNode[] Nodes { get; set; }
        public string ScenarioFile { get; set; }
    }

    public class CwApp
    {
        [DataMember(Name="image")]
        public string Image { get; set; }
        
        [DataMember(Name="port")]
        public int Port { get; set; }
        
        [DataMember(Name="scenarioAppId")]
        public int ScenarioAppId { get; set; }
        
        [DataMember(Name="replicas")]
        public CwRepicaInfo[] Replicas { get; set; }
        
        protected bool Equals(CwApp other)
        {
            return string.Equals(Image, other.Image, StringComparison.InvariantCultureIgnoreCase) && Port == other.Port && ScenarioAppId == other.ScenarioAppId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CwApp) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Image != null ? StringComparer.InvariantCultureIgnoreCase.GetHashCode(Image) : 0);
                hashCode = (hashCode * 397) ^ Port;
                hashCode = (hashCode * 397) ^ ScenarioAppId;
                return hashCode;
            }
        }
    }

    public class CwNode
    {
        [DataMember(Name="name")]
        public string Name { get; set; }
        [DataMember(Name="host")]
        public string Host { get; set; }
    }

    public class CwRepicaInfo
    {
        [DataMember(Name="node")]
        public string Node { get; set; }
    }
    
    public class CwElapsed
    {
        //[DataMember(Name="elapsed_ms")]
        public long elapsed_ms { get; set; }
    }

    public class CwScheduleInfo
    {
        [DataMember(Name="applications")]
        public CwApp[] Applications { get; set; }
    }
    
    public class EntryPoint
    {
        private static string _gatlingPath;
        private static readonly HttpClient client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        
        public static void Main(string[] argv)
        {
            if (argv.Length < 2)
            {
                Console.WriteLine("Usage: $ cw-genetic {config_file.json} {cw-gatling.exe}");
                return;
            }

            var configFile = argv[0];
            var configText = File.ReadAllText(configFile);
            var config = configText.FromJson<CwConfig>();

            _gatlingPath = argv[1];
            
            var g = new Genetic(config.Apps, config.Nodes, EvaluateGeneration);
            g.MoveNext();
            
            Console.WriteLine(g.Current.Dump());
            return;
        }

        private static long ExecShooting()
        {
            var info = new ProcessStartInfo();
            info.FileName = "./shoot.sh";
            info.Arguments = $"{_gatlingPath}";

            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            var p = Process.Start(info);
            p.WaitForExit();

            string log = File.ReadAllText("./shootreport.log");
            Logger.Log("Shooting report");
            Logger.Log(log);
            string json = log.Split(new []{'\n'}, StringSplitOptions.RemoveEmptyEntries).Last();
            Logger.Log($"Json: {json}");
            Logger.Log($"Parsed: {json.FromJson<CwElapsed>().Dump()}");
            
            return json.FromJson<CwElapsed>().elapsed_ms;
        }

        private static async Task SendJsonRequestAsync(string url, string requestData)
        {
            var content = new StringContent(requestData, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            var responseString = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception("Not 200");
            Logger.Log($"HTTP response: {responseString}");
        }

        private static void ExecScheduling(CwApp[] applications)
        {
            var requestData = new CwScheduleInfo { Applications = applications }.ToJson();
            Logger.Log($"Schedule request data: {requestData}");
            for (int retry = 0; retry < 5; ++retry)
            {
                try
                {
                    Logger.Log($"Scheduling. try: {retry}");
                    SendJsonRequestAsync("http://localhost:13337/schedule", requestData).Wait();
                    Logger.Log("Ok. Scheduled.");
                    break;
                }
                catch (Exception)
                {
                    Logger.Log("Exception occured while scheduling");
                }
            }
        }

        private static long EvaluateGene(Gene gene)
        {
            CwApp[] apps = gene.GeneItems
                .GroupBy(g => g.App.CreateCopy(), g => g.Node.CreateCopy())
                .Select(g =>
                {
                    g.Key.Replicas = g
                        .Where(n => n.Name != null)
                        .Select(n => new CwRepicaInfo {Node = n.Name})
                        .ToArray();
                    return g.Key;
                })
                .ToArray();
            
            ExecScheduling(apps);
            
            return ExecShooting();
        }
        
        private static IEnumerable<long> EvaluateGeneration(Generation generation)
        {
            return generation.Genes.Select(EvaluateGene);
        }
    }
}