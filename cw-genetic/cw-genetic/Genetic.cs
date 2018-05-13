using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ServiceStack.Text;

namespace cw_genetic
{
    public class GeneItem
    {
        public CwApp App { get; }
        public CwNode Node { get; }

        public GeneItem(CwApp app, CwNode node)
        {
            App = app;
            Node = node;
        }
    }
    
    public class Gene
    {
        public IList<GeneItem> GeneItems { get; }
        
        public Gene(IEnumerable<GeneItem> items)
        {
            GeneItems = items.ToList();
        }

        public Gene()
        {
            GeneItems = new List<GeneItem>();
        }
    }

    public class Generation
    {
        public IList<Gene> Genes { get; }

        public Generation(IList<Gene> genes)
        {
            Genes = genes;
        }

        public Generation()
        {
            Genes = new List<Gene>();
        }
    }

    public class EvaluatedGeneration : Generation
    {
        public IList<long> Elapsed { get; set; }
        
        public EvaluatedGeneration() : base()
        {
        }

        public EvaluatedGeneration(IList<Gene> genes) : base(genes)
        {
        }
    }
    
    public class Genetic : IEnumerable<EvaluatedGeneration>, IEnumerator<EvaluatedGeneration>
    {
        public int GenerationSize { get; set; } = 7;
        public int CrossCount { get; set; } = 5;
        public int MaxReplicaCount { get; set; } = 2;
        
        private readonly CwApp[] _applications;
        private readonly CwNode[] _nodes;
        private readonly Func<Generation, IEnumerable<long>> _evalFunc;
        
        private int _iteration = -1;
        private EvaluatedGeneration _currentGeneration;
        
        private static Random _random = new Random();

        public Genetic(CwApp[] apps, CwNode[] nodes, Func<Generation, IEnumerable<long>> evalFunc)
        {
            var nullNode = new CwNode {Host = null, Name = null};
            
            _applications = apps;
            _nodes = new[] { nullNode }.Union(nodes).ToArray();
            _evalFunc = evalFunc;
        }

        private Generation MakeInitialPopulation()
        {
            var generation = new Generation();
            for (int i = 0; i < GenerationSize; ++i)
                generation.Genes.Add(CreateRandomGene());
            Logger.Log($"Initial population created. generationSize: {GenerationSize}");
            return generation;
        }

        private bool IsBadGene(Gene gene)
        {
            if (gene.GeneItems.Count != _applications.Length * MaxReplicaCount)
            {
                Logger.Log(
                    $"Gene suppose to be bad due lack of length: {gene.GeneItems.Count}, needed: {_applications.Length * MaxReplicaCount}");
                return true;
            }
            foreach (var app in _applications)
            {
                if (!gene.GeneItems.Any(g => g.App.Equals(app) && g.Node.Name != null))
                {
                    Logger.Log($"Gene suppose to be bad due to no nodes for image: {app.Image}");
                    return true;
                }
            }
            return false;
        }

        private Gene CreateRandomGene()
        {
            var gene = new Gene();

            while (IsBadGene(gene))
            {
                gene.GeneItems.Clear();
                foreach (var app in _applications)
                    for (int replicaIndex = 0; replicaIndex < MaxReplicaCount; ++replicaIndex)
                    {
                        var node = _nodes[_random.Next() % _nodes.Length];
                        var item = new GeneItem(app, node);
                        gene.GeneItems.Add(item);
                    }
            }
            Logger.Log($"Gene generated. gene: {gene.Dump()}");
            return gene;
        }

        private EvaluatedGeneration EvaluateGeneration(Generation generation)
        {
            return new EvaluatedGeneration(generation.Genes) {
                Elapsed = _evalFunc.Invoke(generation).ToList()
            };
        }

        public bool MoveNext()
        {
            Generation generation;
            if (++_iteration == 0)
                generation = MakeInitialPopulation();
            else
                generation = Evolve(_currentGeneration);
            _currentGeneration = EvaluateGeneration(generation);
            return true;
        }

        public Generation Evolve(EvaluatedGeneration parents)
        {
            return null;
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public EvaluatedGeneration Current => _currentGeneration;

        object IEnumerator.Current
        {
            get { return Current; }
        }
        
        
        public IEnumerator<EvaluatedGeneration> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public void Dispose()
        {
        }
    }
}