using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading;
using ServiceStack;
using ServiceStack.Text;

namespace cw_gatling
{
    public class GatlingResponse
    {
        [DataMember(Name = "elapsed_ms")]
        public long ElapsedMs { get; set; }
    }
    
    public class CwConfig
    {
        public CwApp[] Apps { get; set; }
        public CwNode[] Nodes { get; set; }
        public string ScenarioFile { get; set; }
    }

    public class CwApp
    {
        public string Image { get; set; }
        public int Port { get; set; }
        public int ScenarioAppId { get; set; }
        [DataMember(Name = "actual_port")]
        public string ActualPort { get; set; }
    }

    public class CwNode
    {
        public string Name { get; set; }
        public string Host { get; set; }
    }

    public class CwScene
    {
        public CwScenario[] Scenarios { get; set; }
        public int RequestWorkersCount { get; set; }
    }

    public class CwScenario
    {
        public CwAction[] Actions { get; set; }
    }

    public class CwAction
    {
        public int AppId { get; set; }
        public string Url { get; set; }
    }
    
    public class ShootingPrepared
    {
        public IDictionary<int, CwApp> Applications { get; set; }
        public IEnumerable<CwScenario> Scenarios { get; set; }
        public CwNode[] Nodes { get; set; }
        public int WorkersCount { get; set; }
    }
    
    public class EntryPoint
    {
        public static void Main(string[] argv)
        {
            if (argv.Length < 2)
            {
                Console.WriteLine("Usage: $ cw-gatling {config_file.json} {.runtime_config.json}");
                return;
            }

            var configFile = argv[0];
            var configText = File.ReadAllText(configFile);
            var config = configText.FromJson<CwConfig>();

            var scenarioText = File.ReadAllText(config.ScenarioFile);
            var scene = scenarioText.FromJson<CwScene>();

            var actualAppsText = File.ReadAllText(argv[1]);
            var apps = actualAppsText.FromJson<CwApp[]>();

            var preparation = Prepare(config, scene, apps);
            Shoot(preparation);
            return;
        }

        private static ShootingPrepared Prepare(CwConfig config, CwScene scene, CwApp[] actualApps)
        {
            return new ShootingPrepared
            {
                WorkersCount = scene.RequestWorkersCount,
                Scenarios = scene.Scenarios.ToList(),
                Applications = actualApps.ToDictionary(a => a.ScenarioAppId),
                Nodes = config.Nodes.ToArray()
            };
        }

        private static void Shoot(ShootingPrepared preparedData)
        {
            _scenariosLock = new object();
            _appsLock = new object();
            _nodesLock = new object();
            
            lock (_scenariosLock)
            lock (_appsLock)
            lock(_nodesLock)
            {
                _scenarios = preparedData.Scenarios.ToList().GetEnumerator();
                _apps = preparedData.Applications.CreateCopy();
                _nodes = preparedData.Nodes.ToArray();
            }
            
            Console.WriteLine($"Total scenario count: {preparedData.Scenarios.Count()}");
            
            var stopwatch = Stopwatch.StartNew();
            
            var threads = new Thread[preparedData.WorkersCount];
            for (int i = 0; i < threads.Length; ++i)
            {
                threads[i] = new Thread(InternalShootJob);
                threads[i].Start();
            }
            
            foreach (var thread in threads)
                thread.Join();
            
            stopwatch.Stop();

            var response = new GatlingResponse
            {
                ElapsedMs = stopwatch.ElapsedMilliseconds,
            };
            
            // stdout
            Console.WriteLine(response.ToJson());
        }
        

        private static IEnumerator<CwScenario> _scenarios;
        private static IDictionary<int, CwApp> _apps;
        private static CwNode[] _nodes;
        
        private static object _scenariosLock;
        private static object _appsLock;
        private static object _nodesLock;

        private static Random _random = new Random();

        private static void InternalShootJob()
        {
            while (true)
            {
                CwScenario scenario = null;
                lock (_scenariosLock)
                {
                    if (_scenarios.MoveNext())
                        scenario = _scenarios.Current.CreateCopy();
                    else
                        return;
                }
                foreach (var action in scenario.Actions)
                {
                    int port;
                    lock (_appsLock)
                    {
                        CwApp app;
                        if (!_apps.TryGetValue(action.AppId, out app))
                            throw new ArgumentException("Invalid appId in scenario");
                        port = app.Port;
                    }

                    string host;
                    lock (_nodesLock)
                    {
                        var node = _nodes[_random.Next() % _nodes.Length];
                        host = node.Host;
                    }
                    string url = $"http://{host}:{port}/{action.Url}";

                    Console.WriteLine($"Request: {url}");

                    var stopwatch = Stopwatch.StartNew();
                    
                    var request = WebRequest.Create(url);
                    var responseStream = request.GetResponse().GetResponseStream();
                    if (responseStream == null)
                        throw new Exception("Request failed. url: {url}");
                    var reader = new StreamReader(responseStream);
                    string plainText = reader.ReadToEnd();

                    Console.WriteLine($"Response: {plainText} elapsed:{stopwatch.ElapsedMilliseconds}");
                }
            }
        }
    }
}
