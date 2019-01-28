using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Graphing;
namespace UnityEditor.ShaderGraph
{
    [Serializable]
    class SubGraph : AbstractMaterialGraph
        , IGeneratesBodyCode
        , IGeneratesFunction
    {
        [NonSerialized]
        private SubGraphOutputNode m_OutputNode;
        public SubGraphOutputNode outputNode
        {
            get
            {
                // find existing node
                if (m_OutputNode == null)
                    m_OutputNode = GetNodes<SubGraphOutputNode>().FirstOrDefault();
                return m_OutputNode;
            }
        }
        [NonSerialized]
        List<InputDescriptor> m_Inputs = new List<InputDescriptor>();
      
        [NonSerialized]
        List<InputDescriptor> m_AddedInputs = new List<InputDescriptor>();  

        [NonSerialized]
        List<Guid> m_RemovedInputs = new List<Guid>();

        [NonSerialized]
        List<InputDescriptor> m_MovedInputs = new List<InputDescriptor>();

        [SerializeField]
        List<SerializationHelper.JSONSerializedElement> m_SerializedInputs = new List<SerializationHelper.JSONSerializedElement>();
        
        public IEnumerable<InputDescriptor> inputs => m_Inputs;
        public IEnumerable<InputDescriptor> addedInputs => m_AddedInputs;
        public IEnumerable<Guid> removedInputs => m_RemovedInputs;
        public IEnumerable<InputDescriptor> movedInputs => m_MovedInputs;
        public override void OnAfterDeserialize()
        {
            m_Inputs = SerializationHelper.Deserialize<InputDescriptor>(m_SerializedInputs, GraphUtil.GetLegacyTypeRemapping());

            var nodes = SerializationHelper.Deserialize<INode>(m_SerializableNodes, GraphUtil.GetLegacyTypeRemapping());
            m_Nodes = new List<AbstractMaterialNode>(nodes.Count);
            m_NodeDictionary = new Dictionary<Guid, INode>(nodes.Count);
            foreach (var node in nodes.OfType<AbstractMaterialNode>())
            {
                node.owner = this;
                node.UpdateNodeAfterDeserialization();
                node.tempId = new Identifier(m_Nodes.Count);
                m_Nodes.Add(node);
                m_NodeDictionary.Add(node.guid, node);

                if(m_GroupNodes.ContainsKey(node.groupGuid))
                    m_GroupNodes[node.groupGuid].Add(node);
                else
                    m_GroupNodes.Add(node.groupGuid, new List<AbstractMaterialNode>(){node});
            }

            m_SerializableNodes = null;

            m_Edges = SerializationHelper.Deserialize<IEdge>(m_SerializableEdges, GraphUtil.GetLegacyTypeRemapping());
            m_SerializableEdges = null;
            foreach (var edge in m_Edges)
                AddEdgeToNodeEdges(edge);
            m_OutputNode = null;
        }
        public override void AddNode(INode node)
        {
            var materialNode = node as AbstractMaterialNode;
            if (materialNode != null)
            {
                var amn = materialNode;
                if (!amn.allowedInSubGraph)
                {
                    Debug.LogWarningFormat("Attempting to add {0} to Sub Graph. This is not allowed.", amn.GetType());
                    return;
                }
            }
            base.AddNode(node);
        }
        public void GenerateNodeCode(ShaderGenerator visitor, GraphContext graphContext, GenerationMode generationMode)
        {
            foreach (var node in activeNodes)
            {
                if (node is IGeneratesBodyCode)
                    (node as IGeneratesBodyCode).GenerateNodeCode(visitor, graphContext, generationMode);
            }
        }
        public void GenerateNodeFunction(FunctionRegistry registry, GraphContext graphContext, GenerationMode generationMode)
        {
            foreach (var node in activeNodes)
            {
                node.ValidateNode();
                if (node is IGeneratesFunction)
                    (node as IGeneratesFunction).GenerateNodeFunction(registry, graphContext, generationMode);
            }
        }
        public IEnumerable<IShaderProperty> graphInputs
        {
            get { return properties.OrderBy(x => x.guid); }
        }
        public IEnumerable<MaterialSlot> graphOutputs
        {
            get
            {
                return outputNode != null ? outputNode.graphOutputs : new List<MaterialSlot>();
            }
        }
        public void GenerateSubGraphFunction(string functionName, FunctionRegistry registry, GraphContext graphContext, ShaderGraphRequirements reqs, GenerationMode generationMode)
        {
            registry.ProvideFunction(functionName, s =>
                {
                    s.AppendLine("// Subgraph function");
                    // Generate arguments... first INPUTS
                    var arguments = new List<string>();
                    foreach (var prop in graphInputs)
                        arguments.Add(string.Format("{0}", prop.GetPropertyAsArgumentString()));
                    // now pass surface inputs
                    arguments.Add(string.Format("{0} IN", graphContext.graphInputStructName));
                    // Now generate outputs
                    foreach (var slot in graphOutputs)
                        arguments.Add(string.Format("out {0} {1}", slot.concreteValueType.ToString(outputNode.precision), slot.shaderOutputName));
                    // Create the function protoype from the arguments
                    s.AppendLine("void {0}({1})"
                        , functionName
                        , arguments.Aggregate((current, next) => string.Format("{0}, {1}", current, next)));
                    // now generate the function
                    using (s.BlockScope())
                    {
                        // Just grab the body from the active nodes
                        var bodyGenerator = new ShaderGenerator();
                        GenerateNodeCode(bodyGenerator, graphContext, generationMode);
                        if (outputNode != null)
                            outputNode.RemapOutputs(bodyGenerator, generationMode);
                        s.Append(bodyGenerator.GetShaderString(1));
                    }
                });
        }
        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            // if we are previewing the graph we need to
            // export 'exposed props' if we are 'for real'
            // then we are outputting the graph in the
            // nested context and the needed values will
            // be copied into scope.
            if (generationMode == GenerationMode.Preview)
            {
                foreach (var prop in properties)
                    collector.AddShaderProperty(prop);
            }
            foreach (var node in activeNodes)
            {
                if (node is IGenerateProperties)
                    (node as IGenerateProperties).CollectShaderProperties(collector, generationMode);
            }
        }
        public IEnumerable<PreviewProperty> GetPreviewProperties()
        {
            List<PreviewProperty> props = new List<PreviewProperty>();
            foreach (var node in activeNodes)
                node.CollectPreviewMaterialProperties(props);
            return props;
        }
        public IEnumerable<AbstractMaterialNode> activeNodes
        {
            get
            {
                List<INode> nodes = new List<INode>();
                NodeUtils.DepthFirstCollectNodesFromNode(nodes, outputNode);
                return nodes.OfType<AbstractMaterialNode>();
            }
        }

        new public void OnBeforeSerialize()
        {
            m_SerializableNodes = SerializationHelper.Serialize(GetNodes<INode>());
            m_SerializableEdges = SerializationHelper.Serialize<IEdge>(m_Edges);
            m_SerializedInputs = SerializationHelper.Serialize<InputDescriptor>(m_Inputs);
        }

        
    }
}