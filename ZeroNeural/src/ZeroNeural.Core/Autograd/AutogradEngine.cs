using System;
using System.Collections.Generic;
using ZeroTensor.Core;

namespace ZeroNeural.Core.Autograd
{
    /// <summary>
    /// Executes reverse-mode automatic differentiation over the dynamic computational tape graph.
    /// </summary>
    public static class AutogradEngine
    {
        /// <summary>
        /// Executes reverse-mode automatic differentiation starting from the specified root variable.
        /// </summary>
        public static void Backward(Variable root, Tensor<float>? initialGrad = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            if (initialGrad == null)
            {
                initialGrad = Tensor.Ones(root.Data.Shape.ToArray());
            }

            root.Grad = initialGrad;

            // Topological sort of all creator nodes in the DAG
            var nodes = new List<IOpNode>();
            var visitedNodes = new HashSet<IOpNode>();
            var visitedVars = new HashSet<Variable>();

            void BuildTopologicalOrder(Variable v)
            {
                if (v == null || !visitedVars.Add(v)) return;

                if (v.Creator != null && visitedNodes.Add(v.Creator))
                {
                    foreach (var input in v.Creator.Inputs)
                    {
                        BuildTopologicalOrder(input);
                    }
                    nodes.Add(v.Creator);
                }
            }

            BuildTopologicalOrder(root);

            // Traverse in reverse topological order
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                var gradOutput = node.Output.Grad;
                if (gradOutput != null)
                {
                    node.Backward(gradOutput);
                }
            }
        }

        /// <summary>
        /// Accumulates gradient into a variable, automatically handling shape reductions if broadcasting occurred.
        /// </summary>
        public static void AccumulateGrad(Variable variable, Tensor<float> incomingGrad)
        {
            if (!variable.RequiresGrad || incomingGrad == null) return;

            // Un-broadcast incoming gradient to match variable shape
            var targetGrad = incomingGrad;
            var varShape = variable.Data.Shape;

            if (targetGrad.Shape != varShape)
            {
                targetGrad = ReduceGradToShape(targetGrad, varShape);
            }

            if (variable.Grad == null)
            {
                variable.Grad = targetGrad.Clone();
            }
            else
            {
                variable.Grad = variable.Grad + targetGrad;
            }
        }

        private static Tensor<float> ReduceGradToShape(Tensor<float> grad, TensorShape targetShape)
        {
            int gradRank = grad.Rank;
            int targetRank = targetShape.Rank;

            var current = grad;

            // Step 1: Sum leading prepended broadcast dimensions
            int leadingDims = gradRank - targetRank;
            for (int i = 0; i < leadingDims; i++)
            {
                current = TensorOps.Sum(current, axis: 0, keepDims: false);
            }

            // Step 2: Sum dimensions where target has size 1 but grad has size > 1
            for (int i = 0; i < targetRank; i++)
            {
                if (targetShape[i] == 1 && current.Shape[i] > 1)
                {
                    current = TensorOps.Sum(current, axis: i, keepDims: true);
                }
            }

            return current;
        }
    }
}
