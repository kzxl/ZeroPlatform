using System;
using System.Collections.Generic;
using System.Reflection;
using ZeroNeural.Core.Autograd;

namespace ZeroNeural.Core.nn
{
    /// <summary>
    /// Base class for all neural network modules and layers.
    /// Provides parameter discovery, training/evaluation mode switching, and gradient management.
    /// </summary>
    public abstract class Module
    {
        private bool _isTraining = true;

        /// <summary>
        /// Gets or sets whether the module is currently in training mode (affects Dropout, BatchNorm, etc.).
        /// </summary>
        public bool IsTraining
        {
            get => _isTraining;
            set => SetTraining(value);
        }

        /// <summary>
        /// Sets module and all sub-modules to training mode.
        /// </summary>
        public void Train() => SetTraining(true);

        /// <summary>
        /// Sets module and all sub-modules to evaluation / inference mode.
        /// </summary>
        public void Eval() => SetTraining(false);

        protected virtual void SetTraining(bool training)
        {
            _isTraining = training;
            foreach (var subModule in GetSubModules())
            {
                subModule.SetTraining(training);
            }
        }

        /// <summary>
        /// Executes forward pass through the module.
        /// </summary>
        public abstract Variable Forward(Variable input);

        /// <summary>
        /// Recursively discovers all trainable parameter variables in this module and its submodules.
        /// </summary>
        public virtual IEnumerable<Variable> Parameters()
        {
            var seen = new HashSet<Variable>();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            // Search fields for Variables with RequiresGrad
            foreach (var field in GetType().GetFields(flags))
            {
                if (typeof(Variable).IsAssignableFrom(field.FieldType))
                {
                    if (field.GetValue(this) is Variable v && v.RequiresGrad && seen.Add(v))
                    {
                        yield return v;
                    }
                }
            }

            // Search properties for Variables with RequiresGrad
            foreach (var prop in GetType().GetProperties(flags))
            {
                if (typeof(Variable).IsAssignableFrom(prop.PropertyType) && prop.CanRead)
                {
                    if (prop.GetValue(this) is Variable v && v.RequiresGrad && seen.Add(v))
                    {
                        yield return v;
                    }
                }
            }

            // Search sub-modules recursively
            foreach (var subModule in GetSubModules())
            {
                foreach (var param in subModule.Parameters())
                {
                    if (seen.Add(param))
                    {
                        yield return param;
                    }
                }
            }
        }

        /// <summary>
        /// Discovers all sub-modules contained in fields or properties.
        /// </summary>
        protected virtual IEnumerable<Module> GetSubModules()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var field in GetType().GetFields(flags))
            {
                if (typeof(Module).IsAssignableFrom(field.FieldType))
                {
                    if (field.GetValue(this) is Module m && !ReferenceEquals(m, this))
                    {
                        yield return m;
                    }
                }
                else if (typeof(IEnumerable<Module>).IsAssignableFrom(field.FieldType))
                {
                    if (field.GetValue(this) is IEnumerable<Module> modules)
                    {
                        foreach (var m in modules)
                        {
                            if (m != null && !ReferenceEquals(m, this)) yield return m;
                        }
                    }
                }
            }

            foreach (var prop in GetType().GetProperties(flags))
            {
                if (typeof(Module).IsAssignableFrom(prop.PropertyType) && prop.CanRead)
                {
                    if (prop.GetValue(this) is Module m && !ReferenceEquals(m, this))
                    {
                        yield return m;
                    }
                }
            }
        }

        /// <summary>
        /// Resets the gradients of all trainable parameters in this module.
        /// </summary>
        public void ZeroGrad()
        {
            foreach (var param in Parameters())
            {
                param.ZeroGrad();
            }
        }
    }
}
