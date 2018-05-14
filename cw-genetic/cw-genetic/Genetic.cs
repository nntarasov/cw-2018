using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ServiceStack;
using ServiceStack.Text;

namespace cw_genetic
{
    public class GeneItem
    {
        public CwApp App { get; }
        public CwNode Node { get; set; }

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
        public int GenerationSize { get; set; } = 5;
        public int MaxReplicaCount { get; set; } = 2;

        public double MutationProbability
        {
            get { return _mutationProbability; }
            set
            {
                if (value > 1.0 || value < 0)
                    throw new ArgumentOutOfRangeException();
                _mutationProbability = value;
            }
        }

        private readonly CwApp[] _applications;
        private readonly CwNode[] _nodes;
        private readonly Func<Generation, IEnumerable<long>> _evalFunc;
        private double _mutationProbability = 0.05;
        
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
            if (gene == null)
                return true;
            
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
            DumpGeneration(_currentGeneration, _iteration);
            return true;
        }

        /// <summary>
        /// Dumps best child in generation to file ./generated/gen{iteration}.json
        /// </summary>
        private void DumpGeneration(EvaluatedGeneration generation, int iteration)
        {
            int bestGeneIndex = 0;
            for (int i = 0; i < generation.Elapsed.Count; ++i)
                if (generation.Elapsed[bestGeneIndex] > generation.Elapsed[i])
                    bestGeneIndex = i;
            string contents = generation.Genes[bestGeneIndex].ToJson();
            var iterText = (iteration < 10 ? "0" : "") + iteration.ToString();

            string path = $"./generated/gen{iterText}.json";
            Logger.Log($"Writing best generation gene. path: {path}. score: {generation.Elapsed[bestGeneIndex]}");
            File.WriteAllText(path, contents);

            string fullDumpText = generation.ToJson();
            string fullDumpPath = $"./generated/full{iterText}.json";
            Logger.Log(($"Writing full dump. path: {fullDumpPath}"));
            File.WriteAllText(fullDumpPath, fullDumpText);
        }

        private Gene Crossingover(Gene first, Gene second)
        {
            int pivotIndex = _random.Next(1, first.GeneItems.Count - 1);
            int whoIsFirst = _random.Next(1, 2);

            var gene = new Gene();

            if (whoIsFirst == 2)
            {
                Gene temp = first;
                first = second;
                second = temp;
            }

            var firstPart = first.GeneItems.Take(pivotIndex).ToList();
            var secondPart = second.GeneItems.Skip(pivotIndex).ToList();
            foreach (var item in firstPart.Union(secondPart))
                gene.GeneItems.Add(item);

            var mutationFactor = _random.NextDouble() <= _mutationProbability;
            if (mutationFactor)
            {
                int position = _random.Next() % gene.GeneItems.Count;
                int nodeIndex = _random.Next() % _nodes.Length;
                gene.GeneItems[position].Node = _nodes[nodeIndex];
            }
            return gene;
        }

        private Generation Evolve(EvaluatedGeneration parents)
        {
            double avgElapsed = parents.Elapsed.Average();

            var avgDiff = parents.Elapsed.Select(e => avgElapsed - e).ToList();
            double avgDiffMin = avgDiff.Min();
            var transformedDiff = avgDiff.Select(d => d - avgDiffMin).ToList();

            double transformedDiffSum = transformedDiff.Sum();
            var probabilities = transformedDiff.Select(d => d / transformedDiffSum).ToList();
            
            Logger.Log($"Evolution. Elapsed array: {parents.Elapsed.Dump()}");
            Logger.Log($"Evolution. Probabilities: {probabilities.Dump()}");

            var distributions = probabilities.ToList();
            double distribValue = 0.0;
            for (int i = 0; i < distributions.Count; ++i)
            {
                distribValue += distributions[i];
                distributions[i] = distribValue;
            }

            var children = new Generation();
            for (int i = 0; i < GenerationSize; ++i)
            {
                // Select first parent
                double randValue = _random.NextDouble();

                int parentIndex;
                for (parentIndex = 0; parentIndex < distributions.Count; ++parentIndex)
                    if (distributions[parentIndex] >= randValue) break;

                Gene parentOneGene = parents.Genes[parentIndex];
                Logger.Log($"Evolution: First parent: {parentOneGene.Dump()}");

                // Select second parent
                int firstParentIndex = parentIndex;
                while (parentIndex == firstParentIndex)
                {
                    randValue = _random.NextDouble();
                    for (parentIndex = 0; parentIndex < distributions.Count; ++parentIndex)
                        if (distributions[parentIndex] >= randValue) break;
                }
                Gene parentTwoGene = parents.Genes[parentIndex];
                Logger.Log($"Evolution: Second parent: {parentTwoGene.Dump()}");
                
                // Crossing-over
                Gene childGene = null;
                
                while (IsBadGene(childGene))
                    childGene = Crossingover(parentOneGene, parentOneGene);
                
                Logger.Log($"Evolution: = child : {childGene.Dump()}");
                children.Genes.Add(childGene);
            }
            return children;
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