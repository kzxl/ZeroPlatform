using System;
using System.Collections.Generic;
using ZeroNeural.Core.Autograd;

namespace ZeroNeural.Core.nn
{
    /// <summary>
    /// A sequential container that chains modules in pipeline order.
    /// Passes the output of each module as the input to the next.
    /// </summary>
    public class Sequential : Module
    {
        private readonly List<Module> _modules = new List<Module>();

        /// <summary>
        /// Gets the child modules contained in this sequential pipeline.
        /// </summary>
        public IReadOnlyList<Module> Modules => _modules;

        public Sequential(params Module[] modules)
        {
            if (modules != null)
            {
                _modules.AddRange(modules);
            }
        }

        public void Add(Module module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            _modules.Add(module);
        }

        public override Variable Forward(Variable input)
        {
            var current = input;
            for (int i = 0; i < _modules.Count; i++)
            {
                current = _modules[i].Forward(current);
            }
            return current;
        }

        protected override IEnumerable<Module> GetSubModules() => _modules;
    }
}
